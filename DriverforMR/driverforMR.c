#include "ntddk.h"
#include "..\Headers\driverforMR.h"
#include <string.h>
#include <stdio.h>

#pragma pack(1)

typedef struct _SEGMENT_DESCRIPTOR_TABLE {
	USHORT Limit;
	ULONG Base;
}SEGMENT_DESCRIPTOR_TABLE, *PSEGMENT_DESCRIPTOR_TABLE;

typedef struct _SEGMENT_DESCRIPTOR_INFO{
	ULONG Base;
	ULONG Limit;
	UCHAR AccessByte;
	UCHAR Flags;
}SEGMENT_DESCRIPTOR_INFO, *PSEGMENT_DESCRIPTOR_INFO;

typedef struct _HISTORY_OBJECT {
	LIST_ENTRY ListEntry;
	ULONG StartAddress;
	ULONG Length;		// The first byte is flag and the following bytes are used for hashing.
	PVOID Buffer;
}HISTORY_OBJECT, *PHISTORY_OBJECT;

typedef struct _BACKUP_OBJECT {
	ULONG OriginalEthread;
	ULONG OriginalEprocess;
	ULONG OriginalCR3;
	ULONG OriginalHyperPte;
	PSEGMENT_DESCRIPTOR_INFO pSegmentInfoForFS;	// It's for the Current Processor's KPCR[Processor Control Region].
}BACKUP_OBJECT, *PBACKUP_OBJECT;

typedef struct _REQUIRED_OFFSET_ENTRY {
	LIST_ENTRY ListEntry;
	struct _REQUIRED_OFFSET Required;
} REQUIRED_OFFSET_ENTRY, *PREQUIRED_OFFSET_ENTRY;

typedef struct _TARGET_PROCESS {
	ULONG ProcessId;
	PEPROCESS pEprocess;
	ULONG pVadRoot;
	PMDL pUsingMdl;
	LIST_ENTRY HistoryHead;
}TARGET_PROCESS, *PTARGET_PROCESS;

typedef struct _DEVICE_EXTENSION {
	LIST_ENTRY MessageListHead;
	KSPIN_LOCK MessageLock;
	PETHREAD pCommunicationThread;
	KSEMAPHORE CommunicationThreadSemaphore;
	PIRP PendingIRP;
	KSPIN_LOCK PendingIRPLock; 
	KEVENT PendingIRPEvent;
	BOOLEAN bTerminate;
	PTARGET_PROCESS pTargetProcess;
	PBACKUP_OBJECT pBackupObject;
	LIST_ENTRY RequiredOffsetCache;
	KEVENT RequiredOffsetEvent;
} DEVICE_EXTENSION, *PDEVICE_EXTENSION;

typedef struct _MESSAGE_ENTRY {
	LIST_ENTRY ListEntry;
	PMESSAGE_FORM pMessageForm;
} MESSAGE_ENTRY, *PMESSAGE_ENTRY;


#pragma pack()
/////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////
DRIVER_INITIALIZE DriverEntry;
DRIVER_DISPATCH DispatchRoutine;
DRIVER_DISPATCH DispatchRead;
//DRIVER_DISPATCH DispatchWrite;
DRIVER_DISPATCH DispatchControl;
DRIVER_CANCEL CancelMessageIRP;
DRIVER_UNLOAD UnLoad;

//NTSTATUS WorkerStarter(PDEVICE_EXTENSION, PMESSAGE_FORM);
NTSTATUS ConnectWithApplication(PDEVICE_EXTENSION);
NTSTATUS InitializeTargetProcess(PDEVICE_EXTENSION, USHORT, PWCHAR);
NTSTATUS GetKernelObjectDumper(PDEVICE_EXTENSION, PWCHAR, ULONG);
BOOLEAN MessageMaker(USHORT, PVOID, ULONG);
LONG GetRequiredOffsets(PWCHAR, PWCHAR); 
VOID CachingRequiredOffset(PDEVICE_EXTENSION, PREQUIRED_OFFSET);
VOID RemoveTargetProcess(PDEVICE_EXTENSION);

// THREAD.
KSTART_ROUTINE CommunicationThread;
//KSTART_ROUTINE WorkerThread;

// For Test....
//VOID TestForMessageQueuing(ULONG);
VOID ShowByteStreamToDbg(PUCHAR, ULONG);
/////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////


WCHAR nameBuffer[] = L"\\Device\\MemoryReviewer";
WCHAR linkBuffer[] = L"\\DosDevices\\MemoryReviewer";
PDEVICE_OBJECT pMyDevice = NULL;

BOOLEAN isStartedUserCommunicationThread = FALSE;


////////////////////////// 세그먼트 셀렉터 다루는 함수 만들어야 함.
//		-> LDT가 좀 이상함.. 하나하나 값 확인해볼 것.
/*
LDT 필드 갖고 있는 오브젝트들:
	_KPROCESS
		+0x01c LdtDescriptor    : _KGDTENTRY	[8]
			+0x000 LimitLow         : Uint2B
			+0x002 BaseLow          : Uint2B
			+0x004 HighWord         : <unnamed-tag>

	_EPROCESS
		+0x144 LdtInformation   : Ptr32 Void 	[4]

	_KPCR
		+0x000 NtTib            : _NT_TIB
		+0x020 Prcb             : Ptr32 _KPRCB
		+0x038 IDT              : Ptr32 _KIDTENTRY
			+0x000 Offset           : Uint2B
			+0x002 Selector         : Uint2B
			+0x004 Access           : Uint2B
			+0x006 ExtendedOffset   : Uint2B
		+0x03c GDT              : Ptr32 _KGDTENTRY
			+0x000 LimitLow         : Uint2B
			+0x002 BaseLow          : Uint2B
			+0x004 HighWord         : <unnamed-tag>
				+0x000 Bytes            : <unnamed-tag>
				+0x000 Bits             : <unnamed-tag>
		+0x040 TSS              : Ptr32 _KTSS
			+0x060 LDT              : Uint2B
*/


PSEGMENT_DESCRIPTOR_INFO SegmentDescriptorParser(USHORT SegmentSelector) {
	SEGMENT_DESCRIPTOR_TABLE gdtr;		// LDT 구조 확인 후에도 Limit 값 쓸 일 없으면, 이 구조체 제거 후 pCurrentEntry로 사용.
	PSEGMENT_DESCRIPTOR_INFO pSegmentInfo = NULL;
	PULONG pCurrentEntry = NULL;
	BOOLEAN isLocalDescriptorTable = (SegmentSelector & 0x4) ? TRUE : FALSE;		
	UCHAR dbgPrefix[] = "::: IN SEGMENT DESCRIPTOR PARSER : ";

	SegmentSelector = SegmentSelector >> 3;
	if (SegmentSelector) {
		RtlZeroMemory(&gdtr, sizeof(SEGMENT_DESCRIPTOR_TABLE));
		__asm sgdt gdtr;
		if (gdtr.Base && gdtr.Limit) {
			pSegmentInfo = ExAllocatePool(NonPagedPool, sizeof(SEGMENT_DESCRIPTOR_INFO));
			if (pSegmentInfo) {
			LDTPARSING:
				RtlZeroMemory(pSegmentInfo, sizeof(SEGMENT_DESCRIPTOR_INFO));
				pCurrentEntry = (PULONG)(gdtr.Base + (8 * SegmentSelector));

				// Parsing...
				pSegmentInfo->Limit = (pCurrentEntry[0] & 0xFFFF);
				pSegmentInfo->Limit += ((pCurrentEntry[1]) & 0xF0000);

				pSegmentInfo->Base = ((pCurrentEntry[0] >> 16) & 0xFFFF);
				pSegmentInfo->Base += (((pCurrentEntry[1]) & 0xFF) << 16);
				pSegmentInfo->Base += ((pCurrentEntry[1]) & 0xFF000000);

				pSegmentInfo->AccessByte = (UCHAR)((pCurrentEntry[1] >> 8) & 0xFF);
				pSegmentInfo->Flags = (UCHAR)((pCurrentEntry[1] >> 20) & 0xF);

				if (isLocalDescriptorTable) {
					DbgPrintEx(101, 0, "%sLLLLLLLL DDDDDDDD TTTTTTTT <- Have To Test This ENTRY!!!!");

					// SLDT하면 LDT 내에서의 셀렉터 값[16 Bits]이 나오는 듯.	-> 실제 값 테스트 필요.
					gdtr.Base = pSegmentInfo->Base;
					gdtr.Limit = pSegmentInfo->Limit;

					__asm sldt SegmentSelector;						// 이거 16비트 넣어도 되는지 한 번 더 확인해 보고, do while 문으로 돌리자.    
					SegmentSelector = SegmentSelector >> 3;	// 나중에 LDT 실제 값 확인할 때, 3 Bit에 GDT/LDT 값도 체크해보자.
					isLocalDescriptorTable = FALSE;
					
					goto LDTPARSING;
				}

				// Succeeded to Parse.
				DbgPrintEx(101, 0, "%s[BASE]0x%08X  [LIMIT] 0x%08X  [ACCESS BYTE] 0x%02X  [FLAGS] 0x%01X.", dbgPrefix, pSegmentInfo->Base, pSegmentInfo->Limit, pSegmentInfo->AccessByte, pSegmentInfo->Flags);
			}
			else
				DbgPrintEx(101, 0, "%sFailed to Allocate Pool.", dbgPrefix);
		}
		else
			DbgPrintEx(101, 0, "%sFailed to get the information of GDTR.", dbgPrefix);
	}
	else
		DbgPrintEx(101, 0, "%sThis Selector points to the NULL Entry.", dbgPrefix);

	return pSegmentInfo;
}

//////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////
/////////  메모리 덤핑 이제 들어간다...	-> 루틴 완료할 때마다 위에 함수 선언해야 함.
NTSTATUS ManipulateAddressTables(PDEVICE_EXTENSION pExtension) {
	NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
	ULONG tmp = 0;	// It's for the FS & CR3 Values.
	UCHAR dbgPrefix[] = "::: IN MANIPULATE ADDRESS TABLES : ";


	// Get necessary offsets.
	LONG of_Kprocess = GetRequiredOffsets(L"_KTHREAD", L"Process");
	LONG of_ImageFileName = GetRequiredOffsets(L"_EPROCESS", L"ImageFileName");
	LONG of_DirectoryTableBase = GetRequiredOffsets(L"_KPROCESS", L"DirectoryTableBase");
	LONG of_PrcbData = GetRequiredOffsets(L"_KPCR", L"PrcbData");
	LONG of_CurrentThread = GetRequiredOffsets(L"_KPRCB", L"CurrentThread");

	if ((of_Kprocess == -1) || (of_ImageFileName == -1) || (of_DirectoryTableBase == -1) || (of_PrcbData == -1) || (of_CurrentThread == -1)) {
		DbgPrintEx(101, 0, "%sFailed to get necessary offsets.", dbgPrefix);
		return STATUS_RESOURCE_DATA_NOT_FOUND;
	}


	// Always, Remove the last one.
	if (pExtension->pBackupObject){
		if (pExtension->pBackupObject->pSegmentInfoForFS)
			ExFreePool(pExtension->pBackupObject->pSegmentInfoForFS);
	}
	else {
		pExtension->pBackupObject = ExAllocatePool(NonPagedPool, sizeof(BACKUP_OBJECT));
		if (!pExtension->pBackupObject) {
			DbgPrintEx(101, 0, "%sFailed to Allocate the BACKUP_OBJECT", dbgPrefix);
			return ntStatus;
		}
	}
	RtlZeroMemory(pExtension->pBackupObject, sizeof(BACKUP_OBJECT));


	////////////////////////////////////////////////////////////////////////////
	///////////////////////////			Backup			////////////////////////
	////////////////////////////////////////////////////////////////////////////
	__try {
		// Parse the FS Selector to get the current Processor's KPCR Address.
		__asm {
			push fs;
			pop tmp;
		}
		pExtension->pBackupObject->pSegmentInfoForFS = SegmentDescriptorParser((USHORT)tmp);
		tmp = 0;

		// Backup the CR3.
		__asm {
			push eax;
			mov eax, cr3;
			mov tmp, eax;
			pop eax;
		}
		pExtension->pBackupObject->OriginalCR3 = tmp;

		if (pExtension->pBackupObject->pSegmentInfoForFS && tmp) {
			// Backup the current ETHREAD.
			pExtension->pBackupObject->OriginalEthread = *(PULONG)(pExtension->pBackupObject->pSegmentInfoForFS->Base + of_PrcbData + of_CurrentThread);
			
			// Backup the current thread's KPROCESS.
			pExtension->pBackupObject->OriginalEprocess = *(PULONG)(pExtension->pBackupObject->OriginalEthread + of_Kprocess);

			// For Test...
	/*		DbgPrintEx(101, 0, "%sTHREAD : 0x%08X     BACKED _EPROCESS :0x%08X    CR3 : 0x%08X", dbgPrefix, pExtension->pBackupObject->OriginalEthread, pExtension->pBackupObject->OriginalEprocess, pExtension->pBackupObject->OriginalCR3);
			DbgPrintEx(101, 0, "   ::: Original Process : %s\n", (PUCHAR)((pExtension->pBackupObject->OriginalEprocess) + of_ImageFileName));
			DbgPrintEx(101, 0, "       -> Manipulated to : %s\n", (PUCHAR)((ULONG)(pExtension->pTargetProcess->pEprocess) + of_ImageFileName));

			ExFreePool(pExtension->pBackupObject->pSegmentInfoForFS);
			ExFreePool(pExtension->pBackupObject);
			return STATUS_UNSUCCESSFUL;*/


			// To prevent the Object of the target process from being deleted.
			//		-> Have to Dereference the Object as soon as posible.
			//		-> If it fails, The process is considered to have already terminated.
			//		이거 해제할 때 가끔 블루스크린 뜸-> 좀 놔둬서 왜 뜨는지 확인해보고, 이후에 try로...
			// 이거 Original process를 올렸네... 일단 타겟으로 해두고, 리스토어 만든 후 체크.
			ntStatus = ObReferenceObjectByPointer((PVOID)(pExtension->pTargetProcess->pEprocess), GENERIC_ALL, NULL, KernelMode);		// 이거 원래 프로세스만 락했네
			if (!NT_SUCCESS(ntStatus)) {
				DbgPrintEx(101, 0, "%sFailed to increase the Reference count of the current Process' EPROCESS.", dbgPrefix);
				DbgPrintEx(101, 0, "    -> Maybe the process has already terminated.");
			}

			// Succeeded to BACKUP.
			//ntStatus = STATUS_SUCCESS;
		}
		//else {
		//	ExFreePool(pExtension->pBackupObject);
		//	pExtension->pBackupObject = NULL;
		//}
	}
	__except (EXCEPTION_EXECUTE_HANDLER) {
		DbgPrintEx(101, 0, "%sFailed to backup the original values.", dbgPrefix);
		//if(pExtension->pBackupObject->pSegmentInfoForFS)
		//	ExFreePool(pExtension->pBackupObject->pSegmentInfoForFS);
		//ExFreePool(pExtension->pBackupObject);
		//pExtension->pBackupObject = NULL;
		//return ntStatus;
	}


	////////////////////////////////////////////////////////////////////////////
	////////////////////////		Manipulation		////////////////////////
	////////////////////////////////////////////////////////////////////////////
	if (NT_SUCCESS(ntStatus)) {
		__try {
			// Manipulate the KPROCESS of the current thread.
			*(PULONG)(pExtension->pBackupObject->OriginalEthread + of_Kprocess) = (ULONG)pExtension->pTargetProcess->pEprocess;

			// Manipulate the CR3.		이거 CR3 값 하위 12비트[PCID]가 0이 아니네?? 나중에 TLB 때문에 블루 스크린 뜨는 거 확인하고 다루자...
			tmp = *(PULONG)((ULONG)(pExtension->pTargetProcess->pEprocess) + of_DirectoryTableBase);	// Note it!!! NOT the "EPROC_OFFSET_PageDirectoryPte"
			__asm {
				push eax;
				mov eax, tmp;
				mov cr3, eax;
				pop eax;
			}

			// Succeeded.
			DbgPrintEx(101, 0, "%sOriginal Process : %s\n", dbgPrefix, (PUCHAR)((pExtension->pBackupObject->OriginalEprocess) + of_ImageFileName));
			DbgPrintEx(101, 0, "       -> Manipulated to : %s\n", (PUCHAR)((ULONG)(pExtension->pTargetProcess->pEprocess) + of_ImageFileName));
			DbgPrintEx(101, 0, "%sOriginal CR3 : 0x%08X\n", dbgPrefix, pExtension->pBackupObject->OriginalCR3);
			DbgPrintEx(101, 0, "       -> Changed CR3 : 0x%08X\n", tmp);

			return ntStatus;
		}
		__except (EXCEPTION_EXECUTE_HANDLER) {
			// Restore the KPROCESS of the current thread.
			if ((*(PULONG)(pExtension->pBackupObject->OriginalEthread + of_Kprocess)) == (ULONG)(pExtension->pTargetProcess->pEprocess))
				*(PULONG)(pExtension->pBackupObject->OriginalEthread + of_Kprocess) = pExtension->pBackupObject->OriginalEprocess;

			// Decrease the Reference count that had been increased.
			ObDereferenceObject((PVOID)(pExtension->pBackupObject->OriginalEprocess));

			DbgPrintEx(101, 0, "%sFailed to manipulate.", dbgPrefix);
			ntStatus = STATUS_UNSUCCESSFUL;
		}
	}

	// Failed.
	if(pExtension->pBackupObject->pSegmentInfoForFS)
			ExFreePool(pExtension->pBackupObject->pSegmentInfoForFS);
	ExFreePool(pExtension->pBackupObject);
	pExtension->pBackupObject = NULL;

	return ntStatus;
}
///////////////////////////////////// 위에 조작함수는 완성은 헀으나, 에러 없나 한번 쫙 보고... 아래 Restore 작성 후 테스트해야 함.

VOID RestoreAddressTables() {
	PDEVICE_EXTENSION pExtension = pMyDevice->DeviceExtension;
	UCHAR dbgPrefix[] = "::: IN RESTORE ADDRESS TABLES : ";
	ULONG OriginalCr3 = 0;

	// Get necessary offsets.
	LONG of_Kprocess = GetRequiredOffsets(L"_KTHREAD", L"Process");
	if (of_Kprocess == -1) {
		DbgPrintEx(101, 0, "%sFailed to get necessary offsets.", dbgPrefix);
		return;
	}


	if (pExtension && pExtension->pBackupObject) {
		// Restore the CR3.
		OriginalCr3 = pExtension->pBackupObject->OriginalCR3;
		__try {
			__asm {
				push eax;
				mov eax, OriginalCr3;
				mov cr3, eax;
				pop eax;
			}
		}
		__except (EXCEPTION_EXECUTE_HANDLER) {
			DbgPrintEx(101, 0, "%sFailed to restore CR3.", dbgPrefix);
		}

		__try {
			// Restore the KPROCESS of the current thread.
			*(PULONG)(pExtension->pBackupObject->OriginalEthread + of_Kprocess) = pExtension->pBackupObject->OriginalEprocess;

		}
		__except (EXCEPTION_EXECUTE_HANDLER) {
			DbgPrintEx(101, 0, "%sFailed to restore the KPROCESS of the current thread.", dbgPrefix);
		}
		// Decrease the Reference count that had been increased.
		// 	-> 이거 예외 처리 해야 함!!!	-> 그리고 가능한 빨리 해제하라는데.. 다시 Manipulate()로 옮겨야 하나.. 오류확인하자.
		// 오류 확인해 보고 위에 트라이문 안으로 넣자.
		ObDereferenceObject((PVOID)(pExtension->pTargetProcess->pEprocess));



		//Remove the BACKUP_OBJECT.
		if (pExtension->pBackupObject->pSegmentInfoForFS)
			ExFreePool(pExtension->pBackupObject->pSegmentInfoForFS);
		ExFreePool(pExtension->pBackupObject);
		pExtension->pBackupObject = NULL;

		DbgPrintEx(101, 0, "%sThe Manipulations were restored as mush as possible.", dbgPrefix);
	}
}

// 이게 원본.. -> 락이랑 언락 내부에서 각자 Manipulate하는 거를 한번에 하는 걸로 바꾸자...
//	-> 어짜피 덤프할 버퍼를 넌페이지드풀에 만들었으며, 
//	1024바이트 크기로 짤라서 들어오니 메시지 보낼 때 내 유저 인터페이스가 변경된 주소 테이블을 사용할 리도 없음.
//PVOID LockAndMapMemory(ULONG StartAddress, ULONG Length, LOCK_OPERATION Operation) {
//	PVOID mappedAddress = NULL;
//	PDEVICE_EXTENSION pExtension = pMyDevice->DeviceExtension;
//	ULONG backupESP = 0;
//	ULONG backupEBP = 0;
//
//	// Exchange the Vad & PDT
//	if (pExtension && (NT_SUCCESS(ManipulateAddressTables(pExtension)))) {
//
//		// Allocate the MDL.
//		pExtension->pTargetObject->pUsingMdl = MmCreateMdl(NULL, (PVOID)StartAddress, (SIZE_T)Length);
//		if ((pExtension->pTargetObject->pUsingMdl) != NULL) {
//
//			// Lock the MDL.
//			__try {
//				MmProbeAndLockPages(pExtension->pTargetObject->pUsingMdl, KernelMode, Operation);
//			}
//			__except (EXCEPTION_EXECUTE_HANDLER) {
//				// First, Restore the Address tables.
//				RestoreAddressTables();
//
//				IoFreeMdl(pExtension->pTargetObject->pUsingMdl);
//				pExtension->pTargetObject->pUsingMdl = NULL;
//
//				return mappedAddress;
//			}
//
//			// Mapping to System Address.
//			mappedAddress = MmMapLockedPages(pExtension->pTargetObject->pUsingMdl, KernelMode);
//			if (mappedAddress == NULL) {
//				// First, Restore the Address tables.
//				RestoreAddressTables();
//
//				DbgPrintEx(101, 0, "    -> Failed to map to System Address.\n");
//				IoFreeMdl(pExtension->pTargetObject->pUsingMdl);
//				pExtension->pTargetObject->pUsingMdl = NULL;
//
//				return mappedAddress;
//			}
//		}
//
//		// Always Restore.
//		RestoreAddressTables();
//	}
//
//	return mappedAddress;
//}
//
//
//VOID UnMapAndUnLockMemory(PVOID mappedAddress) {
//	PDEVICE_EXTENSION pExtension = pMyDevice->DeviceExtension;
//
//	if (pExtension && pExtension->pTargetObject && (NT_SUCCESS(ManipulateAddressTables(pExtension)))) {
//		if (pExtension->pTargetObject->pUsingMdl) {
//
//			// Unmap.
//			if ((pExtension->pTargetObject->pUsingMdl->MdlFlags) & MDL_MAPPED_TO_SYSTEM_VA) {
//				MmUnmapLockedPages(mappedAddress, pExtension->pTargetObject->pUsingMdl);
//			}
//
//			// Unlock the MDL.
//			__try {
//				MmUnlockPages(pExtension->pTargetObject->pUsingMdl);
//
//				// Free the MDL.
//				IoFreeMdl(pExtension->pTargetObject->pUsingMdl);
//				pExtension->pTargetObject->pUsingMdl = NULL;
//			}
//			__except (EXCEPTION_EXECUTE_HANDLER) {
//				// First, Restore the Address tables.
//				RestoreAddressTables();
//
//				return;
//				//				DbgPrintEx(101, 0, "[ERROR] Failed to unlock the MDL...\n");
//				// In this case, just proceed...
//			}
//
//		}
//
//		RestoreAddressTables();
//	}
//}
//// 이게 원본... -> 버퍼를 콜러에서 만드는 걸로 변경.
//PUCHAR MemoryDumping(ULONG StartAddress, ULONG Length) {
//	PUCHAR memoryDump = NULL;
//	PVOID mappedAddress = NULL;
//	NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
//
//
//	DbgPrintEx(101, 0, "::: Dumping the Address : 0x%08X [%X]\n", StartAddress, Length);
//
//	// Allocate the Pool before corrupt the current VAD & PDT.
//	memoryDump = ExAllocatePool(NonPagedPool, Length);
//	if (memoryDump == NULL) {
//		DbgPrintEx(101, 0, "    -> Failed to allocate pool for dumping the memory...\n");
//	}
//	else {
//		RtlZeroMemory(memoryDump, Length);
//
//		//mappedAddress = LockAndMapMemory(StartAddress, Length, IoReadAccess);
//		if (mappedAddress != NULL) {
//			__try {
//				RtlCopyMemory(memoryDump, mappedAddress, Length);
//				ntStatus = STATUS_SUCCESS;
//			}
//			__except (EXCEPTION_EXECUTE_HANDLER) {
//				ntStatus = STATUS_UNSUCCESSFUL;
//			}
//
//			//UnMapAndUnLockMemory(mappedAddress);
//		}
//		if (!NT_SUCCESS(ntStatus)) {
//			DbgPrintEx(101, 0, "[ERROR] Failed to copy.\n");
//
//			ExFreePool(memoryDump);
//			memoryDump = NULL;
//		}
//
//	}
//
//	return memoryDump;
//}

// 아래 두 함수 내부에서 각자 ManipulateAddress하던 걸 MemoryDumping에서 한번에 하는 걸로 변경..
PVOID LockAndMapMemory(ULONG StartAddress, ULONG Length, LOCK_OPERATION Operation, PUCHAR dbgPrefix) {
	PVOID mappedAddress = NULL;
	PDEVICE_EXTENSION pExtension = pMyDevice->DeviceExtension;

	if (pExtension && pExtension->pTargetProcess) {
		// Create a MDL.
		pExtension->pTargetProcess->pUsingMdl = MmCreateMdl(NULL, (PVOID)StartAddress, (SIZE_T)Length);
		if (pExtension->pTargetProcess->pUsingMdl) {
			// Lock the MDL.
			__try {
				MmProbeAndLockPages(pExtension->pTargetProcess->pUsingMdl, KernelMode, Operation);
			}
			__except (EXCEPTION_EXECUTE_HANDLER) {
				if (GetExceptionCode() == STATUS_ACCESS_VIOLATION) {
					// 이거 대상 주소를 포함하는 페이지가 갖는 접근 권한이, LOCK_OPERATION으로 지정한 동작을 지원하지 않을 때.
					//		-> PTE에 있는 플래그 값 조정하고 다시 시도하는 걸로...
					DbgPrintEx(101, 0, "%sThis page can't be written.", dbgPrefix);
				}
				else {
					DbgPrintEx(101, 0, "%sFailed to Lock the MDL.", dbgPrefix);
				}

				IoFreeMdl(pExtension->pTargetProcess->pUsingMdl);
				pExtension->pTargetProcess->pUsingMdl = NULL;
				DbgPrintEx(101, 0, "%sFailed to Lock the MDL.", dbgPrefix);
				return mappedAddress;
			}

			// Map to the System Address.
			mappedAddress = MmMapLockedPages(pExtension->pTargetProcess->pUsingMdl, KernelMode);
			if (mappedAddress) 
				DbgPrintEx(101, 0, "%sSucceeded to Map the target Address to the System Address.", dbgPrefix);
			else {
				IoFreeMdl(pExtension->pTargetProcess->pUsingMdl);
				pExtension->pTargetProcess->pUsingMdl = NULL;

				DbgPrintEx(101, 0, "%sFailed to Map the MDL to the System Address.", dbgPrefix);
			}
		}
		else
			DbgPrintEx(101, 0, "%sFailed to Create a MDL for the Target Address.", dbgPrefix);
	}

	return mappedAddress;
}


VOID UnMapAndUnLockMemory(PVOID mappedAddress, PUCHAR dbgPrefix) {
	PDEVICE_EXTENSION pExtension = pMyDevice->DeviceExtension;

	if (pExtension && pExtension->pTargetProcess && pExtension->pTargetProcess->pUsingMdl) {
		// Unmap from the System Address.
		if ((pExtension->pTargetProcess->pUsingMdl->MdlFlags) & MDL_MAPPED_TO_SYSTEM_VA) 
			MmUnmapLockedPages(mappedAddress, pExtension->pTargetProcess->pUsingMdl);
		
		// Unlock the MDL.
		__try {
			MmUnlockPages(pExtension->pTargetProcess->pUsingMdl);

			// Free the MDL.
			IoFreeMdl(pExtension->pTargetProcess->pUsingMdl);
			pExtension->pTargetProcess->pUsingMdl = NULL;

			DbgPrintEx(101, 0, "%sSucceeded to Free the MDL for the Target Address.", dbgPrefix);
		}
		__except (EXCEPTION_EXECUTE_HANDLER) {
			DbgPrintEx(101, 0, "%sFailed to Unlock the MDL.", dbgPrefix);
		}
	}
}

NTSTATUS MemoryDumping(ULONG StartAddress, ULONG Length, PUCHAR buffer) {
	PVOID mappedAddress = NULL;
	NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
	UCHAR dbgPrefix[] = "::: MEMORY DUMPING : ";

	if (buffer && (Length <= 1024)) {
		// For Test...
		//if (test == 0x63)
		//	return ntStatus;
		//while ((--Length)) {
		//	buffer[Length] = test;
		//}
		//ntStatus = STATUS_SUCCESS;
		DbgPrintEx(101, 0, "%s0x%08X [0x%X]\n",dbgPrefix, StartAddress, Length);


		ntStatus = ManipulateAddressTables(pMyDevice->DeviceExtension);
		if (NT_SUCCESS(ntStatus)) {

			// 락이랑 언락까지 다 하긴했다. 테스트해보자.
			// 에러는 안난다만, UI랑 노트패드랑 같은값이 나왔다 첫 4바이트.. 원래 같은듯.. 다른값찾아서 해보자.
			//		-> 다른 값 확인 완료. 그대로 EPROCESS 출력하는 함수 만들고 진행.
			mappedAddress = LockAndMapMemory(StartAddress, Length, IoReadAccess, dbgPrefix);
			if (mappedAddress != NULL) {
				__try {
					RtlCopyMemory(buffer, mappedAddress, Length);
					ntStatus = STATUS_SUCCESS;
				}
				__except (EXCEPTION_EXECUTE_HANDLER) {
					DbgPrintEx(101, 0, "::: ERROR occured while memory dumping : 0x%08X", GetExceptionCode());
				}
				UnMapAndUnLockMemory(mappedAddress, dbgPrefix);
			}

			RestoreAddressTables();
		}


	}

	return ntStatus;
}


/////////// 이거 롱사이즈 테스트, 에러 테스트 모두 완료.
NTSTATUS GetKernelObjectDumper(PDEVICE_EXTENSION pExtension, PWCHAR ObjectName, ULONG Size) {
	NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
	ULONG startAddress = 0;
	PMESSAGE_FORM pMessageForm = NULL;
	ULONG dumpedSize = 0;
	USHORT i = 0;
	USHORT count = Size / 1024;
	UCHAR dbgPrefix[] = "::: IN KERNEL OBJECT DUMPER : ";

	// 오브젝트 종류 하나씩 입력해야 함.
	if (wcsncmp(ObjectName, L"_EPROCESS", 128) == 0) {
		if(pExtension->pTargetProcess->pEprocess)
			startAddress = (ULONG)(pExtension->pTargetProcess->pEprocess);
	}
	// else if...

	if (startAddress) {
		do{
			pMessageForm = ExAllocatePool(NonPagedPool, sizeof(MESSAGE_FORM));
			if (pMessageForm) {
				RtlZeroMemory(pMessageForm, sizeof(MESSAGE_FORM));
				pMessageForm->Address = startAddress + (i * 1024);
				pMessageForm->Type = SIN_GET_KERNEL_OBJECT_CONTENTS;
				pMessageForm->Size = ((i == count) ? (Size % 1024) : 1024);

				ntStatus = MemoryDumping(startAddress + (i * 1024), pMessageForm->Size, pMessageForm->bMessage);
				if (!NT_SUCCESS(ntStatus)) {
					// Stop & Send the Error Code.
					if (ntStatus == STATUS_RESOURCE_DATA_NOT_FOUND) {
						pMessageForm->Res = (USHORT)(STATUS_RESOURCE_DATA_NOT_FOUND & 0xFFFF);
						ntStatus = STATUS_UNSUCCESSFUL;
					}
					else
						pMessageForm->Res = 0xFFFF;		
				}

				// Always, Send.
				if (!MessageMaker(pMessageForm->Type, pMessageForm, sizeof(MESSAGE_FORM))) 
					ntStatus = STATUS_UNSUCCESSFUL;

				if (NT_SUCCESS(ntStatus)) {
					DbgPrintEx(101, 0, "%sSucceeded to send [%d/%d].", dbgPrefix, i + 1, count + 1); 
					pMessageForm = NULL;
				}
				else { 
					DbgPrintEx(101, 0, "%sERROR OCCURED [%d/%d].", dbgPrefix, i + 1, count + 1);
					break; 
				}
			}
		} while ((i++) < count);
	}

	return ntStatus;
}

//// 이거 없앨 듯....
//ULONG GetMemoryDump(ULONG ctlCode, PMMVAD pVad, PUCHAR buffer) {
//	PUCHAR dumpStartAddress = 0;
//	ULONG dumpLength = 0;
//	UCHAR secondType = buffer[0];
//	PUCHAR dumpBuffer = NULL;
//
//	////////////////////////////////////////// VAD 가 해제됐는지 확인하는 플래그 찾으면 검사후 들어갈 것.
//	//if (pVad != NULL) {
//	//	switch (ctlCode) {
//	//	case IOCTL_MEMORY_DUMP_RANGE:
//	//		dumpStartAddress = (PUCHAR)pVad;
//	//		dumpLength = *(PULONG)(buffer + 1);
//	//		break;
//	//	case IOCTL_MEMORY_DUMP_PAGE:
//	//		dumpStartAddress = (PUCHAR)pVad;
//	//		dumpLength = 4096;
//	//		break;
//	//	case IOCTL_MEMORY_DUMP_VAD:
//	//		dumpStartAddress = (PUCHAR)pVad;
//	//		dumpLength = sizeof(MMVAD);
//	//		break;
//	//	case IOCTL_MEMORY_DUMP_ULONG_FLAGS:
//	//		if (secondType == 0)
//	//			dumpStartAddress = (PUCHAR)&(pVad->LongFlags);
//	//		else if (secondType == 2)
//	//			dumpStartAddress = (PUCHAR)&(pVad->LongFlags2);
//	//		else if (secondType == 3)
//	//			dumpStartAddress = (PUCHAR)&(pVad->LongFlags3);
//
//	//		dumpLength = 4;
//	//		break;
//	//	case IOCTL_MEMORY_DUMP_CA:
//	//		if ((pVad->pSubsection) && (pVad->pSubsection->pControlArea)) {
//	//			dumpStartAddress = (PUCHAR)(pVad->pSubsection->pControlArea);
//	//			dumpLength = sizeof(CONTROL_AREA);
//	//		}
//	//		break;
//	//	case IOCTL_MEMORY_DUMP_SEGMENT:
//	//		if ((pVad->pSubsection) && (pVad->pSubsection->pControlArea) && (pVad->pSubsection->pControlArea->pSegment)) {
//	//			dumpStartAddress = (PUCHAR)(pVad->pSubsection->pControlArea->pSegment);
//	//			dumpLength = sizeof(SEGMENT);
//	//		}
//	//		break;
//	//	case IOCTL_MEMORY_DUMP_SUBSECTION:
//	//		if ((pVad->pSubsection) && (secondType > 0)) {		// the First Subsection's index is 1.
//	//			dumpStartAddress = (PUCHAR)(pVad->pSubsection);
//	//			while (--secondType) {
//	//				dumpStartAddress = (PUCHAR)(((PMSUBSECTION)dumpStartAddress)->pNextSubsection);
//	//				if (dumpStartAddress == NULL) // if already NULL before detect the target subsection, Processit as Fail.
//	//					break;
//	//			}
//	//			dumpLength = sizeof(SUBSECTION);
//	//		}
//	//		break;
//	//	default:
//	//		dumpLength = 0;
//	//		break;
//	//	}
//	//}
//
//	if ((dumpStartAddress != NULL) && (dumpLength != 0)) {
//		RtlZeroMemory(buffer, 4100);
//
//		dumpBuffer = MemoryDumping((ULONG)dumpStartAddress, dumpLength);
//		if (dumpBuffer == NULL) {
//			return 0;
//		}
//		else {
//			*(PULONG)buffer = (ULONG)dumpStartAddress;
//			__try {
//				RtlCopyMemory(buffer + 4, dumpBuffer, dumpLength);
//				ExFreePool(dumpBuffer);
//				dumpBuffer = NULL;
//				return (dumpLength + 4);
//			}
//			__except (EXCEPTION_EXECUTE_HANDLER) {
//				//				DbgPrintEx(101, 0, "[ERROR] Failed to Dump [Exception Code : 0x%08X].\n", GetExceptionCode());
//			}
//		}
//	}
//
//	ExFreePool(dumpBuffer);
//	return 0;
//}


//////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////

//// HANDLE Ver.
//VOID RemoveTargetProcess(PDEVICE_EXTENSION pExtension) {
//	UCHAR dbgPrefix[] = "::: IN Remove Target Process : ";
//
//	if (pExtension->pTargetProcess) {
//		// Restore the Manipulated memory of the target process : HISTORY.
//		// ...
//		DbgPrintEx(101, 0, "%sRestore the Manipulated memory...", dbgPrefix);
//
//		// Decrease the Reference Count.	-> 이거 예외 처리 해야 함!!!	-> 그리고 가능한 빨리 해제하라는데.. 다시 Manipulate()로 옮겨야 하나..
//		ObDereferenceObject(pExtension->pTargetProcess->pEprocess);
//		ZwClose(pExtension->pTargetProcess->hProcess);
//		ExFreePool(pExtension->pTargetProcess);
//		pExtension->pTargetProcess = NULL;
//	}
//}
//
//// HANDLE Ver.
//NTSTATUS InitializeTargetProcess(PDEVICE_EXTENSION pExtension, USHORT targetId, PWCHAR targetName) {
//	NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
//	UCHAR dbgPrefix[] = "::: IN Initialize Target Process : ";
//	OBJECT_ATTRIBUTES objAttr;
//	HANDLE hProcess = NULL;
//	PEPROCESS pEprocess = NULL;
//	CLIENT_ID cid;
//
//	// Check necessary offsets.
//	LONG of_VadRoot = GetRequiredOffsets(L"_EPROCESS", L"VadRoot");
//	if (of_VadRoot == -1) {
//		DbgPrintEx(101, 0, "%sFailed to get necessary offsets...", dbgPrefix);
//		return STATUS_RESOURCE_DATA_NOT_FOUND;
//	}
//
//	// Get HANDLE of target process.
//	cid.UniqueProcess = targetId;
//	cid.UniqueThread = 0;
//	InitializeObjectAttributes(&objAttr, NULL, OBJ_KERNEL_HANDLE, NULL, NULL);
//	
//	ntStatus = ZwOpenProcess(&hProcess, GENERIC_ALL, &objAttr, &cid);
//	if (NT_SUCCESS(ntStatus)) {
//		ntStatus = ObReferenceObjectByHandle(hProcess, GENERIC_ALL, NULL, KernelMode, &pEprocess, NULL);
//		if (NT_SUCCESS(ntStatus)) {
//			DbgPrintEx(101, 0, "%sPROCESS OBJECT: 0x%08X", dbgPrefix, (ULONG)pEprocess);
//			DbgPrintEx(101, 0,"%sPROCESS HANDLE : 0x%08X", dbgPrefix, (ULONG)hProcess);
//			
//			// Set Create the Target Object. 
//			pExtension->pTargetProcess = ExAllocatePool(NonPagedPool, sizeof(TARGET_PROCESS));
//			if (pExtension->pTargetProcess) {
//				RtlZeroMemory(pExtension->pTargetProcess, sizeof(TARGET_PROCESS));
//
//				pExtension->pTargetProcess->hProcess = hProcess;
//				pExtension->pTargetProcess->pEprocess = pEprocess;
//				pExtension->pTargetProcess->pVadRoot = (ULONG)pEprocess + of_VadRoot;
//				pExtension->pTargetProcess->ProcessId = targetId;
//				InitializeListHead(&(pExtension->pTargetProcess->HistoryHead));
//
//				ntStatus = STATUS_SUCCESS;
//			}
//			else {				
//				ObDereferenceObject(pEprocess);
//				ZwClose(hProcess);
//				DbgPrintEx(101, 0, "%sFailed to Allocate pool for the TARGET_OBJECT...", dbgPrefix);
//
//				ntStatus = STATUS_UNSUCCESSFUL;
//			}
//		}
//		else {
//			ZwClose(hProcess);
//			DbgPrintEx(101, 0, "%sFailed to get Reference object by Handle.", dbgPrefix);
//		}
//	}
//	else
//		DbgPrintEx(101, 0, "%sFailed to get handle of %ws. [CODE : 0x%08X]", dbgPrefix, targetName, (ULONG)ntStatus);
//
//	return ntStatus;
//}
//


VOID RemoveTargetProcess(PDEVICE_EXTENSION pExtension) {
	UCHAR dbgPrefix[] = "::: IN Remove Target Process : ";

	if (pExtension->pTargetProcess) {
		// Restore the Manipulated memory of the target process : HISTORY.
		// ...

		DbgPrintEx(101, 0, "%sRestore the Manipulated memory...", dbgPrefix);

		ExFreePool(pExtension->pTargetProcess);
		pExtension->pTargetProcess = NULL;
	}
}

// targetPID's first byte is the flags for History Function.		-> 항상 히스토리 만드는 걸로...
NTSTATUS InitializeTargetProcess(PDEVICE_EXTENSION pExtension, USHORT targetId, PWCHAR targetName) {
	NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
	ULONG pFirstEPROCESS = 0;
	ULONG pCurrentEPROCESS = 0;
	BOOLEAN isDetected = FALSE;
	UCHAR dbgPrefix[] = "::: IN Initialize Target Process : ";

	// Get necessary offsets.
	LONG of_UniqueProcessId = GetRequiredOffsets(L"_EPROCESS", L"UniqueProcessId");
	LONG of_ActiveProcessLinks = GetRequiredOffsets(L"_EPROCESS", L"ActiveProcessLinks");
	LONG of_VadRoot = GetRequiredOffsets(L"_EPROCESS", L"VadRoot");

	if ((of_UniqueProcessId == -1) || (of_ActiveProcessLinks == -1) || (of_VadRoot == -1)) {
		DbgPrintEx(101, 0, "%sFailed to get necessary offsets...", dbgPrefix);
		return STATUS_RESOURCE_DATA_NOT_FOUND;
	}


	// Search for the Target Process.
	pFirstEPROCESS = (ULONG)IoGetCurrentProcess();
	if (pFirstEPROCESS == NULL) {
		DbgPrintEx(101, 0, "%sFailed to get the first process...", dbgPrefix);
		return ntStatus;
	}

	pCurrentEPROCESS = pFirstEPROCESS;
	do {
		if (*(PULONG)(pCurrentEPROCESS + of_UniqueProcessId) == targetId) {
			isDetected = TRUE;
			break;
		}
		pCurrentEPROCESS = (PEPROCESS)((*(PULONG)(pCurrentEPROCESS + of_ActiveProcessLinks)) - of_ActiveProcessLinks);
	} while (pCurrentEPROCESS != pFirstEPROCESS);

	if (isDetected) {
		DbgPrintEx(101, 0, "%sTarget EPROCESS is at 0x%08X.", dbgPrefix, pCurrentEPROCESS);

		// Set the Target Object. 
		pExtension->pTargetProcess = ExAllocatePool(NonPagedPool, sizeof(TARGET_PROCESS));
		if (pExtension->pTargetProcess) {
			RtlZeroMemory(pExtension->pTargetProcess, sizeof(TARGET_PROCESS));

			pExtension->pTargetProcess->pEprocess = pCurrentEPROCESS;
			pExtension->pTargetProcess->pVadRoot = pCurrentEPROCESS + of_VadRoot;
			pExtension->pTargetProcess->ProcessId = targetId;
			InitializeListHead(&(pExtension->pTargetProcess->HistoryHead));

			ntStatus = STATUS_SUCCESS;
		}
		else {
			DbgPrintEx(101, 0, "%sFailed to Allocate pool for the TARGET_OBJECT...", dbgPrefix);
			ntStatus = STATUS_UNSUCCESSFUL;
		}
	}
	else
		DbgPrintEx(101, 0, "%sTarget PID is not exist...", dbgPrefix);

	return ntStatus;
}


VOID CachingRequiredOffset(PDEVICE_EXTENSION pExtension, PREQUIRED_OFFSET response) {
	PREQUIRED_OFFSET_ENTRY pRequiredEntry = NULL;

	pRequiredEntry = ExAllocatePool(NonPagedPool, sizeof(REQUIRED_OFFSET_ENTRY));
	if (pRequiredEntry) {
		RtlZeroMemory(pRequiredEntry, sizeof(REQUIRED_OFFSET_ENTRY));
		RtlCopyMemory(&(pRequiredEntry->Required), response, sizeof(REQUIRED_OFFSET));
		InsertTailList(&(pExtension->RequiredOffsetCache), &(pRequiredEntry->ListEntry));

		// For Test....
		DbgPrintEx(101, 0, "::: CACHED ::: %ws!%ws  is at  0x%03X.", pRequiredEntry->Required.ObjectName, pRequiredEntry->Required.FieldName, pRequiredEntry->Required.Offset);
	}

	return;
}

LONG GetRequiredOffsets(PWCHAR ObjectName, PWCHAR FieldName) {
	PDEVICE_EXTENSION pExtension = (PDEVICE_EXTENSION)(pMyDevice->DeviceExtension);
	PVOID pRequired = pExtension->RequiredOffsetCache.Flink;
	
	while (&(pExtension->RequiredOffsetCache) != pRequired) {
		if ((wcsncmp(ObjectName, ((PREQUIRED_OFFSET_ENTRY)pRequired)->Required.ObjectName, wcsnlen_s(ObjectName, 128)) == 0) &&
			(wcsncmp(FieldName, ((PREQUIRED_OFFSET_ENTRY)pRequired)->Required.FieldName, wcsnlen_s(FieldName, 128)) == 0)) {
			return (LONG)(((PREQUIRED_OFFSET_ENTRY)pRequired)->Required.Offset);
		}
		else
			pRequired = ((PREQUIRED_OFFSET_ENTRY)pRequired)->ListEntry.Flink;
	}

	//////////////////////////////////////////////////////////////////////////////////
	//////////////		GET THE OFFSET FROM UI[KernelObjects.cs]		//////////////
	//////////////////////////////////////////////////////////////////////////////////
	pRequired = ExAllocatePool(NonPagedPool, sizeof(REQUIRED_OFFSET));
	if (pRequired) {
		RtlZeroMemory(pRequired, sizeof(REQUIRED_OFFSET));
		RtlCopyMemory(((PREQUIRED_OFFSET)pRequired)->ObjectName, ObjectName, (wcsnlen_s(ObjectName, 128) + 1) * 2);
		RtlCopyMemory(((PREQUIRED_OFFSET)pRequired)->FieldName, FieldName, (wcsnlen_s(FieldName, 128) + 1) * 2);

		if (MessageMaker(SIN_URGENT_MESSAGE | SIN_GET_REQUIRED_OFFSET, pRequired, sizeof(REQUIRED_OFFSET))) {
			// Wait For the Response Message.
			KeWaitForSingleObject(&(pExtension->RequiredOffsetEvent), Executive, KernelMode, FALSE, NULL);

			if (!IsListEmpty(&(pExtension->RequiredOffsetCache))) {
				pRequired = pExtension->RequiredOffsetCache.Blink;	// The Last Entry.
						
				if ((wcsncmp(ObjectName, ((PREQUIRED_OFFSET_ENTRY)pRequired)->Required.ObjectName, wcsnlen_s(ObjectName, 128)) == 0) &&
					(wcsncmp(FieldName, ((PREQUIRED_OFFSET_ENTRY)pRequired)->Required.FieldName, wcsnlen_s(FieldName, 128)) == 0))
					return (LONG)(((PREQUIRED_OFFSET_ENTRY)pRequired)->Required.Offset);
				else
					DbgPrintEx(101, 0, "::: Failed to get the response of GetRequiredOffset from UI.");
			}
		}
	}
	else 
		DbgPrintEx(101, 0, "::: Failed to send an URGENT MESSAGE for GetRequiredOffset.");

	return -1;
}


BOOLEAN MessageMaker(USHORT messageType, PVOID pMessage, ULONG messageLength) {
	PMESSAGE_ENTRY pMessageEntry = NULL;
	UCHAR dbgPrefix[] = "::: IN Message Maker : ";
	PDEVICE_EXTENSION pExtension = (PDEVICE_EXTENSION)(pMyDevice->DeviceExtension);

	if (pMessage && ((messageLength <= 1024) || (messageLength == sizeof(MESSAGE_FORM)))) {
		// Allocate a MESSAGE_ENTRY.
		pMessageEntry = ExAllocatePool(NonPagedPool, sizeof(MESSAGE_ENTRY));
		if (pMessageEntry) {
			RtlZeroMemory(pMessageEntry, sizeof(MESSAGE_ENTRY));
			if (messageLength == sizeof(MESSAGE_FORM)) 
				pMessageEntry->pMessageForm = pMessage;
			else {
				pMessageEntry->pMessageForm = ExAllocatePool(NonPagedPool, sizeof(MESSAGE_FORM));
				if (pMessageEntry->pMessageForm) {
					RtlZeroMemory(pMessageEntry->pMessageForm, sizeof(MESSAGE_FORM));

					pMessageEntry->pMessageForm->Type = messageType;
					RtlCopyMemory((pMessageEntry->pMessageForm->bMessage), pMessage, messageLength);
				}

			}

			// Queuing...
			if (pMessageEntry->pMessageForm) {
				__try {
					if (messageType & SIN_URGENT_MESSAGE) {
						ExInterlockedInsertHeadList(&(pExtension->MessageListHead), &(pMessageEntry->ListEntry), &(pExtension->MessageLock));
						DbgPrintEx(101, 0, "%sAN URGENT Message is queued...\n", dbgPrefix);
					}
					else
						ExInterlockedInsertTailList(&(pExtension->MessageListHead), &(pMessageEntry->ListEntry), &(pExtension->MessageLock));

					KeReleaseSemaphore(&(pExtension->CommunicationThreadSemaphore), 0, 1, FALSE);
					return TRUE;
				}
				__except (EXCEPTION_EXECUTE_HANDLER) {
					DbgPrintEx(101, 0, "%sFailed to queue a message.\n", dbgPrefix);
					ExFreePool(pMessageEntry->pMessageForm);
				}
			}
			ExFreePool(pMessageEntry);
		}
		
		DbgPrintEx(101, 0, "%sFailed to Allocate the Pool for a MessageEntry", dbgPrefix);
	}

	return FALSE;
}

// It's only for test....
//VOID TestForMessageQueuing(ULONG count) {
//	WCHAR message[] = L"It's for test.";
//	
//	while(count--) {
//		if (!MessageMaker(count, message, sizeof(message)))
//			break;
//	}
//
//	DbgPrintEx(101, 0, " ::: Test Messgae : %ws[%d]\n", message, sizeof(message));
//}

// This Routine is for checking the byte stream.
VOID ShowByteStreamToDbg(PUCHAR pBuffer, ULONG length) {
	UCHAR line[53] = { 0, };	// 16 * 3 + 5
	ULONG i = 0;
	ULONG stored = 0;

	for (i = 0; i < length; i++) {
		stored += _snprintf_s(line + stored, 53, 3, "%02X ", (UCHAR)*(pBuffer + i));

		if ((i + 1) % 16 == 0) {
			DbgPrintEx(101, 0, "%s", line);
			RtlZeroMemory(line, 53);
			stored = 0;
		}
		else {
			if ((i + 1) % 4 == 0)
				stored += _snprintf_s(line + stored, 53, 1, " ");

			if ((i + 1) % 8 == 0)
				stored += _snprintf_s(line + stored, 53, 1, " ");
		}
	}

	if (strlen(line) > 0)
		DbgPrintEx(101, 0, "%s", line);
}


//
//NTSTATUS WorkerStarter(PDEVICE_EXTENSION pExtension, PMESSAGE_FORM pMessageForm) {
//	HANDLE hThread = NULL;
//	OBJECT_ATTRIBUTES objAttr;
//	NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
//	PVOID context = NULL;
//
//	if (pExtension && pMessageForm) {
//		context = ExAllocatePool(NonPagedPool, sizeof(MESSAGE_FORM) + 4);
//		if (context) {
//			RtlCopyMemory(context, &pExtension, 4);
//			RtlCopyMemory((PVOID)((ULONG)context + 4), pMessageForm, sizeof(MESSAGE_FORM));
//
//			InitializeObjectAttributes(&objAttr, NULL, OBJ_KERNEL_HANDLE, NULL, NULL);
//			ntStatus = PsCreateSystemThread(&hThread, THREAD_ALL_ACCESS, &objAttr, NULL, NULL, WorkerThread, context);
//			if (NT_SUCCESS(ntStatus)) {
//				ZwClose(hThread);
//			}
//		}
//	}
//
//	return ntStatus;
//}
//
//VOID WorkerThread(PVOID context) {
//	PDEVICE_EXTENSION pExtension = (PDEVICE_EXTENSION)*((PULONG)context);
//	PMESSAGE_FORM pMessageForm = (PMESSAGE_FORM)((ULONG)context + 4);
//
//	switch (pMessageForm->Type) {
//	case SIN_SET_TARGET_OBJECT:
//		InitializeTargetProcess(pExtension, pMessageForm->Res, pMessageForm->uMessage);
//		break;
//	default:
//		break;
//	}
//
//	ExFreePool(context);
//}

VOID CommunicationThread(PVOID context) {
	PDEVICE_EXTENSION pExtension = (PDEVICE_EXTENSION)context;
	KIRQL kIrql;
	PIRP pIrp;
	PMESSAGE_ENTRY pMessageEntry;
	UCHAR dbgPrefix[] = "::: IN Communication Thread : ";
	PMDL pMdl;

	while (TRUE) {
		pMessageEntry = pIrp = pMdl = NULL;

		KeWaitForSingleObject(&(pExtension->CommunicationThreadSemaphore), Executive, KernelMode, FALSE, NULL);
		if (pExtension->bTerminate) {
			DbgPrintEx(101, 0, "%sTerminated...\n", dbgPrefix);
			PsTerminateSystemThread(STATUS_SUCCESS);
		}
		
		KeAcquireSpinLock(&(pExtension->MessageLock), &kIrql);
		if (!IsListEmpty(&(pExtension->MessageListHead))) {
			pMessageEntry = (PMESSAGE_ENTRY)RemoveHeadList(&(pExtension->MessageListHead));
			KeReleaseSpinLock(&(pExtension->MessageLock), kIrql);
			if (pMessageEntry != &(pExtension->MessageListHead)) {
				if (pMessageEntry && (pMessageEntry->pMessageForm)) {
					// LOOP for Waiting for an IRP.
					while (TRUE) {
						KeWaitForSingleObject(&(pExtension->PendingIRPEvent), Executive, KernelMode, FALSE, NULL);
						if (pExtension->bTerminate) {
							// Restore the Dequeued Message...
							__try {
								ExInterlockedInsertHeadList(&(pExtension->MessageListHead), (PLIST_ENTRY)pMessageEntry, &(pExtension->MessageLock));
							}
							__except (EXCEPTION_EXECUTE_HANDLER) {
								DbgPrintEx(101, 0, "%sFailed to restore the dequeued message...\n", dbgPrefix);
							}

							DbgPrintEx(101, 0, "%sTerminated...\n", dbgPrefix);
							PsTerminateSystemThread(STATUS_SUCCESS);
							return;
						}

						KeAcquireSpinLock(&(pExtension->PendingIRPLock), &kIrql);
						pIrp = pExtension->PendingIRP;
						pExtension->PendingIRP = NULL;
						KeReleaseSpinLock(&(pExtension->PendingIRPLock), kIrql);

						if (pIrp) {
							//////////////////////////////////////////////////////////////////////////
							//////////////////			METHOD_IO_DIRECT			//////////////////
							//////////////////////////////////////////////////////////////////////////
							if ((pIrp->MdlAddress) && (MmGetSystemAddressForMdl(pIrp->MdlAddress)) && (MmGetMdlByteCount(pIrp->MdlAddress) == sizeof(MESSAGE_FORM))) {
								if((pMessageEntry->pMessageForm->Type) & SIN_URGENT_MESSAGE)
									DbgPrintEx(101, 0, "%sAn URGENT IRP is completed...", dbgPrefix);

								RtlCopyMemory(MmGetSystemAddressForMdl(pIrp->MdlAddress), (pMessageEntry->pMessageForm), sizeof(MESSAGE_FORM));
								ExFreePool(pMessageEntry->pMessageForm);
								ExFreePool(pMessageEntry);

								pIrp->IoStatus.Status = STATUS_SUCCESS;
								pIrp->IoStatus.Information = sizeof(MESSAGE_FORM);

								IoCompleteRequest(pIrp, IO_NO_INCREMENT);
								break;
							}
							else {
								// In this Case, Just Continue this Loop...
								DbgPrintEx(101, 0, "%sThe MDL of the Pending IRP is corrupted...\n", dbgPrefix);
								pIrp->IoStatus.Status = STATUS_UNSUCCESSFUL;
								pIrp->IoStatus.Information = 0;

								IoCompleteRequest(pIrp, IO_NO_INCREMENT);
								pIrp = NULL;
							}
						}
					} // Waiting IRP Loop
					continue;
				}
				else {
					if (pMessageEntry) {
						if (pMessageEntry->pMessageForm)
							ExFreePool(pMessageEntry->pMessageForm);
						ExFreePool(pMessageEntry);
						DbgPrintEx(101, 0, "%sThe Buffer of the dequeued Message is corrupted...\n", dbgPrefix);
					}
				}
			}
		}
		else
			KeReleaseSpinLock(&(pExtension->MessageLock), kIrql);
		
		DbgPrintEx(101, 0, "%sThe Message Queue is Empty...\n", dbgPrefix);
	}	// while
}

NTSTATUS ConnectWithApplication(PDEVICE_EXTENSION pExtension) {
	NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
	OBJECT_ATTRIBUTES objAttr;
	HANDLE hThread = NULL;
	UCHAR dbgPrefix[] = "::: IN DRIVER ENTRY : ";

	// Create the Kernel Communication Thread...
	InitializeObjectAttributes(&objAttr, NULL, OBJ_KERNEL_HANDLE, NULL, NULL);
	ntStatus = PsCreateSystemThread(&hThread, GENERIC_ALL, &objAttr, NULL, NULL, CommunicationThread, (PVOID)pExtension);
	if (NT_SUCCESS(ntStatus)) {
		KeInitializeSemaphore(&(pExtension->CommunicationThreadSemaphore), 0, 500);
		KeInitializeEvent(&(pExtension->PendingIRPEvent), SynchronizationEvent, FALSE);

		ntStatus = ObReferenceObjectByHandle(hThread, THREAD_ALL_ACCESS, NULL, KernelMode, &(pExtension->pCommunicationThread), NULL);
		ZwClose(hThread); 

		if (NT_SUCCESS(ntStatus)) {
			// Make ready for the Queues...
			InitializeListHead(&(pExtension->MessageListHead));
			KeInitializeSpinLock(&(pExtension->MessageLock));
			KeInitializeSpinLock(&(pExtension->PendingIRPLock));

			// Required
			InitializeListHead(&(pExtension->RequiredOffsetCache));
			KeInitializeEvent(&(pExtension->RequiredOffsetEvent), SynchronizationEvent, FALSE);

			DbgPrintEx(101, 0, "%sSucceeded to Create the Kernel Communication Thread...\n", dbgPrefix);
		}
		else {
			// Terminate the Communication Thread.
			pExtension->bTerminate = TRUE;
			KeReleaseSemaphore(&(pExtension->CommunicationThreadSemaphore), 0, 1, FALSE);
			KeSetEvent(&(pExtension->PendingIRPEvent), 0, FALSE);
			DbgPrintEx(101, 0, "%sFailed to Create the Kernel Communication Thread...\n", dbgPrefix);
		}
	}

	return ntStatus;
}



///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
NTSTATUS DriverEntry(PDRIVER_OBJECT pDriverObject, PUNICODE_STRING regPath) {
	NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
	ULONG i = 0;
	UNICODE_STRING deviceName;
	UNICODE_STRING linkName;
	UCHAR dbgPrefix[] = "::: IN DRIVER ENTRY : ";

	for (i = 0; i < IRP_MJ_MAXIMUM_FUNCTION; i++)
		pDriverObject->MajorFunction[i] = DispatchRoutine;

	pDriverObject->MajorFunction[IRP_MJ_DEVICE_CONTROL] = DispatchControl;
	pDriverObject->MajorFunction[IRP_MJ_READ] = DispatchRead;
	//pDriverObject->MajorFunction[IRP_MJ_WRITE] = DispatchWrite;
	pDriverObject->DriverUnload = UnLoad;

	RtlInitUnicodeString(&deviceName, nameBuffer);
	RtlInitUnicodeString(&linkName, linkBuffer);

	ntStatus = IoCreateDevice(pDriverObject, sizeof(DEVICE_EXTENSION), &deviceName, SIN_DEV_TYPE, 0, FALSE, &pMyDevice);
	if (NT_SUCCESS(ntStatus)) {
		ntStatus = IoCreateSymbolicLink(&linkName, &deviceName);
		if (NT_SUCCESS(ntStatus)) {
			RtlZeroMemory(pMyDevice->DeviceExtension, sizeof(DEVICE_EXTENSION));

			// Initialize my driver...
			ntStatus = ConnectWithApplication(pMyDevice->DeviceExtension);
			if (NT_SUCCESS(ntStatus)) {
				pMyDevice->Flags |= DO_DIRECT_IO;
				DbgPrintEx(101, 0, "%sSucceeded to load my Driver...\n", dbgPrefix);

				return ntStatus;
			}

			IoDeleteSymbolicLink(&linkName);
		}
		else
			DbgPrintEx(101, 0, "%sFailed to Create the Symbolic Link...\n", dbgPrefix);
		
		IoDeleteDevice(pMyDevice);
	}
	else
		DbgPrintEx(101, 0, "%sFailed to Create the Device...\n", dbgPrefix);

	return ntStatus;
}

// INPUT BUFFER : MDL, OUTPUT BUFFER : SystemBuffer
NTSTATUS DispatchControl(PDEVICE_OBJECT pDeviceObject, PIRP pIrp) {
	PDEVICE_EXTENSION pExtension = (PDEVICE_EXTENSION)(pDeviceObject->DeviceExtension);
	PIO_STACK_LOCATION irpStack = NULL;
	PMESSAGE_FORM pMessageForm = NULL;
	NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
	UCHAR dbgPrefix[] = "::: IN CONTROL Dispatcher : ";
	USHORT ctlCode = 0;

	if (pIrp->MdlAddress && (MmGetMdlByteCount(pIrp->MdlAddress) == sizeof(MESSAGE_FORM))) {
		irpStack = IoGetCurrentIrpStackLocation(pIrp);
		if (irpStack) {
			ctlCode = (USHORT)(((irpStack->Parameters.DeviceIoControl.IoControlCode) >> 2) & 0xFFF);
			switch (ctlCode) {
			case SIN_TERMINATE_USER_THREAD:
				if (isStartedUserCommunicationThread) {
					isStartedUserCommunicationThread = FALSE;
					DbgPrintEx(101, 0, "%sMarked the TERMINATE_USER_THREAD...\n", dbgPrefix);

					ntStatus = STATUS_SUCCESS;
				}
				break;
			case SIN_DESELECT_TARGET_PROCESS:
				if (pExtension->pTargetProcess) {
					RemoveTargetProcess(pExtension);
				}

				// Always, SUCCESS;
				ntStatus = STATUS_SUCCESS;
				break;
			///////////////////////////////////////////////////////////////////////////////
			///////////////////////////////////////////////////////////////////////////////
			///////////////////////////////////////////////////////////////////////////////
			case SIN_RESPONSE_REQUIRED_OFFSET:
				pMessageForm = MmGetSystemAddressForMdl(pIrp->MdlAddress);
				if (pMessageForm->Res != 0xFFFF)
					CachingRequiredOffset(pExtension, &(pMessageForm->Required));
				
				// Always, AWAKE.
				if(!IsListEmpty(&(pExtension->RequiredOffsetEvent.Header.WaitListHead)))
					KeSetEvent(&(pExtension->RequiredOffsetEvent), 0, FALSE);	
		
				ntStatus = STATUS_SUCCESS;
				break;
			case SIN_SELECT_TARGET_PROCESS:
				pMessageForm = MmGetSystemAddressForMdl(pIrp->MdlAddress);
				ntStatus = InitializeTargetProcess(pExtension, pMessageForm->Res, pMessageForm->uMessage);
				if (ntStatus == STATUS_RESOURCE_DATA_NOT_FOUND) {
					pMessageForm->Res = (USHORT)(STATUS_RESOURCE_DATA_NOT_FOUND & 0xFFFF);
					ntStatus = STATUS_UNSUCCESSFUL;
				}
				break;
			case SIN_GET_KERNEL_OBJECT_CONTENTS:
				if (pExtension->pTargetProcess) {
					pMessageForm = MmGetSystemAddressForMdl(pIrp->MdlAddress);
					if ((pMessageForm->Size) && (wcsnlen_s(pMessageForm->uMessage, 128))) {
						ntStatus = GetKernelObjectDumper(pExtension, pMessageForm->uMessage, pMessageForm->Size);
					}
				}
				break;
			case SIN_GET_BYTE_STREAM:
				pMessageForm = MmGetSystemAddressForMdl(pIrp->MdlAddress);
				if (pMessageForm && (pMessageForm->Size != 0) && (((pMessageForm->Address != 0) || (wcslen(pMessageForm->uMessage) > 0)))) {
					//
				}
				break;
			default:
				break;
			}
		}
	}

	//pIrp->IoStatus.Status = ntStatus;	-> This 'pIrp->IoStatus' Field is not associated with the IRP_MJ_DEVICE_CONTROL.
	IoCompleteRequest(pIrp, IO_NO_INCREMENT);
	return ntStatus;
}

NTSTATUS DispatchRead(PDEVICE_OBJECT pDeviceObject, PIRP pIrp) {
	PUCHAR pBuffer = NULL;
	KIRQL kIrql;
	PDEVICE_EXTENSION pExtension = (PDEVICE_EXTENSION)(pDeviceObject->DeviceExtension);
	PDRIVER_CANCEL pOldCancelRoutine = NULL;
	UCHAR dbgPrefix[] = "::: IN READ Dispatcher : ";


	//////////////////////////////////////////////////////////////////////////
	//////////////////			METHOD_BUFFERED				//////////////////
	//////////////////////////////////////////////////////////////////////////
	//PIO_STACK_LOCATION irpStack = IoGetCurrentIrpStackLocation(pIrp);
	//pBuffer = pIrp->UserBuffer;
	//if (pBuffer) {
	//	length = irpStack->Parameters.Read.Length;
	//	DbgPrintEx(101, 0, "::: BUFFER : 0%08X[%d] VALUE : 0x%08X\n", (ULONG)pBuffer, length, *((PULONG)pBuffer));

	//}

	//////////////////////////////////////////////////////////////////////////
	//////////////////			METHOD_IO_DIRECT			//////////////////
	//////////////////////////////////////////////////////////////////////////
	if (pIrp->MdlAddress) {
		pBuffer = MmGetSystemAddressForMdl(pIrp->MdlAddress);
		if (pBuffer && (MmGetMdlByteCount(pIrp->MdlAddress) == sizeof(MESSAGE_FORM))) {
			RtlZeroMemory(pBuffer, sizeof(MESSAGE_FORM));

			if (!isStartedUserCommunicationThread) {
				// For Synchronization with the User Communication Thread...
				*(PUSHORT)pBuffer = INITIALIZE_COMMUNICATION;
				isStartedUserCommunicationThread = TRUE;

				// For Test...
				//DbgPrintEx(101, 0, "IN READ MESSAGE_FORM SIZE : %d - %d", sizeof(MESSAGE_FORM), MmGetMdlByteCount(pIrp->MdlAddress));
				//TestForMessageQueuing(10);
				//wcscpy(((PMESSAGE_FORM)pBuffer)->uMessage, L"HI, I Received your First message...");	// 72

				pIrp->IoStatus.Information = sizeof(MESSAGE_FORM);
				pIrp->IoStatus.Status = STATUS_SUCCESS;
				IoCompleteRequest(pIrp, IO_NO_INCREMENT);
				return STATUS_SUCCESS;
			}
			else {
				KeAcquireSpinLock(&(pExtension->PendingIRPLock), &kIrql);
				if (pExtension->PendingIRP) {
					// A Pending IRP is already exist : Maybe, This case will not be occurred.
					pIrp->IoStatus.Information = 0;
					pIrp->IoStatus.Status = STATUS_UNSUCCESSFUL;
					KeReleaseSpinLock(&(pExtension->PendingIRPLock), kIrql);
					DbgPrintEx(101, 0, "%sA Pending IRP is already exist...", dbgPrefix);

					IoCompleteRequest(pIrp, IO_NO_INCREMENT);
					return STATUS_UNSUCCESSFUL;
				}
				else {
					IoMarkIrpPending(pIrp);
					pExtension->PendingIRP = pIrp;

					// Register my Cancel Routine for the IRP and Check whether the Cancel Routine had already started.
					pOldCancelRoutine = IoSetCancelRoutine(pIrp, CancelMessageIRP);
					if (pOldCancelRoutine == NULL) {
						if (pIrp->Cancel) {
							pOldCancelRoutine = IoSetCancelRoutine(pIrp, NULL);
							if (pOldCancelRoutine) {
								pExtension->PendingIRP = NULL;
								pIrp->IoStatus.Status = STATUS_CANCELLED;
								KeReleaseSpinLock(&(pExtension->PendingIRPLock), kIrql);

								IoCompleteRequest(pIrp, IO_NO_INCREMENT);
								return STATUS_PENDING;
							}
							else {
								// In this case, The IRP will be cancelled in my Cancel Routine...
							}
						}
						else {
							// Pending...
							DbgPrintEx(101, 0, "%sA Pending IRP is occurred...", dbgPrefix);
							KeSetEvent(&(pExtension->PendingIRPEvent), 0, FALSE);
						}
					}

					KeReleaseSpinLock(&(pExtension->PendingIRPLock), kIrql);
					return STATUS_PENDING;
				}
			}
		}
	}

	DbgPrintEx(101, 0, "%sThe MDL of the IRP is corrupted...%d\n", dbgPrefix, MmGetMdlByteCount(pIrp->MdlAddress));
	pIrp->IoStatus.Information = 0;
	pIrp->IoStatus.Status = STATUS_UNSUCCESSFUL;

	IoCompleteRequest(pIrp, IO_NO_INCREMENT);
	return STATUS_UNSUCCESSFUL;
}

//
//NTSTATUS DispatchWrite(PDEVICE_OBJECT pDeviceObejct, PIRP pIrp) {
//	PDEVICE_EXTENSION pExtension = (PDEVICE_EXTENSION)(pDeviceObejct->DeviceExtension);
//	NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
//	PMESSAGE_FORM pMessage = NULL;
//	UCHAR dbgPrefix[] = "::: IN WRITE Dispatcher : ";
//
//	if (pIrp->MdlAddress && (MmGetMdlByteCount(pIrp->MdlAddress) == sizeof(MESSAGE_FORM))) {
//		pMessage = MmGetSystemAddressForMdl(pIrp->MdlAddress);
//		if (pMessage) {
//			switch (pMessage->Type) {
//			case SIN_RESPONSE_REQUIRED_OFFSET:
//				DbgPrintEx(101, 0, "%sSIN_RESPONSE_REQUIRED_OFFSET", dbgPrefix);
//				//ShowByteStreamToDbg(&(pMessage->Required), 516);
//				/*if (pMessageForm->Res != 0xFFFF) {
//				CachingRequiredOffset(pExtension, &(pMessageForm->Required));
//				}*/
//				if(!IsListEmpty(&(pExtension->RequiredOffsetEvent.Header.WaitListHead)))
//					KeSetEvent(&(pExtension->RequiredOffsetEvent), 0, FALSE);	// Always, AWAKE.
//
//				ntStatus = STATUS_SUCCESS;
//				break;
//			default:
//				break;
//			}
//		}
//	}
//
//	IoCompleteRequest(pIrp, IO_NO_INCREMENT);
//	return ntStatus;
//}

VOID UnLoad(PDRIVER_OBJECT pDriverObject) {
	UNICODE_STRING linkName;
	PDEVICE_EXTENSION pExtension = (PDEVICE_EXTENSION)(pDriverObject->DeviceObject->DeviceExtension);
	KIRQL kIrql;
	PVOID pListEntry = NULL;
	UCHAR dbgPrefix[] = "::: IN UNLOAD ROUTINE : ";

	if (!IsListEmpty(&(pExtension->RequiredOffsetEvent.Header.WaitListHead)))
		KeSetEvent(&(pExtension->RequiredOffsetEvent), 0, FALSE);

	// Terminate the Kernel Communication Thread.
	pExtension->bTerminate = TRUE;

	if (!IsListEmpty(&(pExtension->PendingIRPEvent.Header.WaitListHead)))
		KeSetEvent(&(pExtension->PendingIRPEvent), 0, FALSE); 
	KeReleaseSemaphore(&(pExtension->CommunicationThreadSemaphore), 0, 1, FALSE);
	KeWaitForSingleObject(pExtension->pCommunicationThread, Executive, KernelMode, FALSE, NULL);
	DbgPrintEx(101, 0, "%sTerminated the Communication Thread...\n", dbgPrefix);

	// Clear the Message Queue.
	KeAcquireSpinLock(&(pExtension->MessageLock), &kIrql);
	while (!IsListEmpty(&(pExtension->MessageListHead))) {
		pListEntry = RemoveHeadList(&(pExtension->MessageListHead));
		if (((PMESSAGE_ENTRY)pListEntry)->pMessageForm)
			ExFreePool(((PMESSAGE_ENTRY)pListEntry)->pMessageForm);

		ExFreePool(pListEntry);
	}
	KeReleaseSpinLock(&(pExtension->MessageLock), kIrql);
	DbgPrintEx(101, 0, "%sClear the Message List...\n", dbgPrefix);

	// Delete Resources.
	while (!IsListEmpty(&(pExtension->RequiredOffsetCache))) {
		ExFreePool((PREQUIRED_OFFSET_ENTRY)RemoveHeadList(&(pExtension->RequiredOffsetCache)));
	}
	DbgPrintEx(101, 0, "%sClear the Required Offset Cache...", dbgPrefix);

	// Remove Target Process.
	if(pExtension->pTargetProcess)
		RemoveTargetProcess(pExtension);
	
	// Delete my DEVICE_OBJECT.
	RtlInitUnicodeString(&linkName, linkBuffer);
	IoDeleteSymbolicLink(&linkName);
	if (pDriverObject->DeviceObject)
		IoDeleteDevice(pDriverObject->DeviceObject);
	DbgPrintEx(101, 0, "%sCompleted...\n", dbgPrefix);

	return;
}

NTSTATUS DispatchRoutine(PDEVICE_OBJECT pDeviceObject, PIRP pIrp) {
	DbgPrintEx(101, 0, "MJ : %d\n", (IoGetCurrentIrpStackLocation(pIrp)->MajorFunction));
	pIrp->IoStatus.Status = STATUS_SUCCESS;

	IoCompleteRequest(pIrp, IO_NO_INCREMENT);
	return STATUS_SUCCESS;
}

VOID CancelMessageIRP(PDEVICE_OBJECT pDeviceObject, PIRP pIrp) {
	KIRQL kIrql;
	UCHAR dbgPrefix[] = "::: IN CANCEL ROUTINE : ";
	PDEVICE_EXTENSION pExtension = NULL;

	// Cancel the Global Cancel Lock
	IoReleaseCancelSpinLock(pIrp->CancelIrql);

	if (pDeviceObject && pDeviceObject->DeviceExtension) {
		pExtension = (PDEVICE_EXTENSION)(pDeviceObject->DeviceExtension);

		// EVENT.
		if (!IsListEmpty(&(pExtension->RequiredOffsetEvent.Header.WaitListHead)))
			KeSetEvent(&(pExtension->RequiredOffsetEvent), 0, FALSE);

		KeAcquireSpinLock(&(pExtension->PendingIRPLock), &kIrql);
		if (pExtension->PendingIRP) {
			pExtension->PendingIRP = NULL;

			pIrp->IoStatus.Status = STATUS_CANCELLED;
			pIrp->IoStatus.Information = 0;
			KeReleaseSpinLock(&(pExtension->PendingIRPLock), kIrql);
			DbgPrintEx(101, 0, "%sA Pending IRP is Cancelled...\n", dbgPrefix);

			IoCompleteRequest(pIrp, IO_NO_INCREMENT);
		}
		else
			KeReleaseSpinLock(&(pExtension->PendingIRPLock), kIrql);
	}
	
	return;
}
