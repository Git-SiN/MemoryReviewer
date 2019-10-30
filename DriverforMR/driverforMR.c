#include "ntddk.h"
#include "..\Headers\driverforMR.h"
#include <string.h>
#include <stdio.h>

#pragma pack(1)
typedef struct _HISTORY_OBJECT {
	LIST_ENTRY ListEntry;
	ULONG StartAddress;
	ULONG Length;		// The first byte is flag and the following bytes are used for hashing.
	PVOID Buffer;
}HISTORY_OBJECT, *PHISTORY_OBJECT;

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

BOOLEAN MessageMaker(USHORT, PVOID, ULONG);
//NTSTATUS WorkerStarter(PDEVICE_EXTENSION, PMESSAGE_FORM);
NTSTATUS ConnectWithApplication(PDEVICE_EXTENSION);
VOID CachingRequiredOffset(PDEVICE_EXTENSION, PREQUIRED_OFFSET);
LONG GetRequiredOffsets(PWCHAR, PWCHAR);
NTSTATUS InitializeTargetProcess(PDEVICE_EXTENSION, USHORT, PWCHAR);

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



// targetPID's first byte is the flags for History Function.		-> 항상 히스토리 만드는 걸로...
NTSTATUS InitializeTargetProcess(PDEVICE_EXTENSION pExtension, USHORT targetId, PWCHAR targetName) {
	LONG offset = 0;

	DbgPrintEx(101, 0, "::: TARGET PROCESS : %ws [%d]", targetName, targetId);

	// Test for URGENT_GET_REQUIRED_OFFSET..
	offset = GetRequiredOffsets(L"_EPROCESS", L"UniqueProcessId");
	DbgPrintEx(101, 0, "::: GET REQUIRED OFFSET TEST ::: UniqueProcessId : 0x%03X", offset);

	return STATUS_SUCCESS;



	//ULONG pFirstEPROCESS = 0;
	//ULONG pCurrentEPROCESS = 0;
	//NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
	//PTARGET_OBJECT pTargetObject = NULL;
	//BOOLEAN isDetected = FALSE;
	//UCHAR flags = 0;

	//pFirstEPROCESS = (ULONG)IoGetCurrentProcess();
	//if (pFirstEPROCESS == NULL) {
	//	DbgPrintEx(101, 0, "[ERROR] Failed to get the first process...\n");
	//	return ntStatus;
	//}

	//// Separate Flags frome targetPID.
	//flags = (UCHAR)((targetPID & 0xFF000000) >> 24);
	//targetPID &= 0x00FFFFFF;

	//// Find the Target Process.
	//pCurrentEPROCESS = pFirstEPROCESS;
	//do {
	//	if (*(PULONG)(pCurrentEPROCESS + EPROC_OFFSET_UniqueProcessId) == targetPID) {
	//		isDetected = TRUE;
	//		break;
	//	}
	//	else {
	//		pCurrentEPROCESS = (PEPROCESS)((*(PULONG)(pCurrentEPROCESS + EPROC_OFFSET_ActiveProcessLinks)) - EPROC_OFFSET_ActiveProcessLinks);
	//	}
	//} while (pCurrentEPROCESS != pFirstEPROCESS);

	//// Result of the search.
	//if (!isDetected) {
	//	DbgPrintEx(101, 0, "[ERROR] Target PID is not exist...\n");
	//	return ntStatus;
	//}
	//else {
	//	DbgPrintEx(101, 0, "   ::: Target Process's EPROCESS is at 0x%08X\n", pCurrentEPROCESS);

	//	// Set the Target Object. 
	//	pExtension->pTargetObject = ExAllocatePool(NonPagedPool, sizeof(TARGET_OBJECT));
	//	if (pExtension->pTargetObject == NULL) {
	//		DbgPrintEx(101, 0, "[ERROR] Failed to Allocate pool for TARGET_OBJECT...\n");
	//		return ntStatus;
	//	}
	//	pTargetObject = pExtension->pTargetObject;
	//	RtlZeroMemory(pTargetObject, sizeof(TARGET_OBJECT));

	//	pTargetObject->pEprocess = pCurrentEPROCESS;
	//	pTargetObject->pVadRoot = pCurrentEPROCESS + EPROC_OFFSET_VadRoot;
	//	pTargetObject->ProcessId = targetPID;
	//	if (flags & 0x80) {
	//		InitializeListHead(&(pTargetObject->HistoryHead));
	//		pTargetObject->bHistory = TRUE;
	//	}

	//	UserMessageMaker(pExtension, MESSAGE_TYPE_PROCESS_INFO);
	//	UserMessageMaker(pExtension, MESSAGE_TYPE_VAD);
	//	UserMessageMaker(pExtension, MESSAGE_TYPE_HANDLES);
	//	UserMessageMaker(pExtension, MESSAGE_TYPE_WORKINGSET_SUMMARY);

	//	return STATUS_SUCCESS;
	//}
}


VOID CachingRequiredOffset(PDEVICE_EXTENSION pExtension, PREQUIRED_OFFSET response) {
	PREQUIRED_OFFSET_ENTRY pRequiredEntry = NULL;

	pRequiredEntry = ExAllocatePool(NonPagedPool, sizeof(REQUIRED_OFFSET_ENTRY));
	if (pRequiredEntry) {
		RtlZeroMemory(pRequiredEntry, sizeof(REQUIRED_OFFSET_ENTRY));
		RtlCopyMemory(&(pRequiredEntry->Required), response, sizeof(REQUIRED_OFFSET));
		InsertTailList(&(pExtension->RequiredOffsetCache), &(pRequiredEntry->ListEntry));

		// For Test....
		DbgPrintEx(101, 0, "Cached the Offset for %ws!%ws : 0x%03X", pRequiredEntry->Required.ObjectName, pRequiredEntry->Required.FieldName, pRequiredEntry->Required.Offset);
	}

	return;
}

LONG GetRequiredOffsets(PWCHAR ObjectName, PWCHAR FieldName) {
	PDEVICE_EXTENSION pExtension = (PDEVICE_EXTENSION)(pMyDevice->DeviceExtension);
	ULONG i = 0;
	PVOID pRequired = pExtension->RequiredOffsetCache.Flink;
	UCHAR LengthOfObjectName = wcslen(ObjectName);
	
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

	if (pMessage && (messageLength <= 1024)) {
		pMessageEntry = ExAllocatePool(NonPagedPool, sizeof(MESSAGE_ENTRY));
		if (pMessageEntry) {
			RtlZeroMemory(pMessageEntry, sizeof(MESSAGE_ENTRY));
			pMessageEntry->pMessageForm = ExAllocatePool(NonPagedPool, sizeof(MESSAGE_FORM));
			if (pMessageEntry->pMessageForm) {
				RtlZeroMemory(pMessageEntry->pMessageForm, sizeof(MESSAGE_FORM));

				pMessageEntry->pMessageForm->Type = messageType;
				RtlCopyMemory((pMessageEntry->pMessageForm->bMessage), pMessage, messageLength);

				// Queuing...
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
			case SIN_RESPONSE_REQUIRED_OFFSET:
				pMessageForm = MmGetSystemAddressForMdl(pIrp->MdlAddress);
				if (pMessageForm->Res != 0xFFFF)
					CachingRequiredOffset(pExtension, &(pMessageForm->Required));
				
				// Always, AWAKE.
				if(!IsListEmpty(&(pExtension->RequiredOffsetEvent.Header.WaitListHead)))
					KeSetEvent(&(pExtension->RequiredOffsetEvent), 0, FALSE);	
		
				ntStatus = STATUS_SUCCESS;
				break;
			case SIN_SET_TARGET_OBJECT:
				pMessageForm = MmGetSystemAddressForMdl(pIrp->MdlAddress);
				ntStatus = InitializeTargetProcess(pExtension, pMessageForm->Res, pMessageForm->uMessage);
				break;
			case SIN_GET_KERNEL_OBJECT:
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
	PMESSAGE_ENTRY pMessageEntry = NULL;
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
		pMessageEntry = (PMESSAGE_ENTRY)RemoveHeadList(&(pExtension->MessageListHead));
		if (pMessageEntry != &(pExtension->MessageListHead)) {
			if (pMessageEntry->pMessageForm)
				ExFreePool(pMessageEntry->pMessageForm);

			ExFreePool(pMessageEntry);
		}
	}
	KeReleaseSpinLock(&(pExtension->MessageLock), kIrql);
	DbgPrintEx(101, 0, "%sClear the Message List...\n", dbgPrefix);

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
