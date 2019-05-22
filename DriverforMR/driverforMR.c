#include "ntddk.h"
#include "..\Headers\driverforMR.h"

// MESSAGE_TYPE
#define		INITIALIZE_COMMUNICATION			0x80000000
#define		DRIVER_UNLOADED						0xF0000000

#pragma pack(1)
typedef struct _DEVICE_EXTENSION {
	LIST_ENTRY MessageListHead;
	KSPIN_LOCK MessageLock;
	PETHREAD pCommunicationThread;
	KSEMAPHORE CommunicationThreadSemaphore;
	PIRP PendingIRP;
	KSPIN_LOCK PendingIRPLock; 
	KEVENT PendingIRPEvent;
	BOOLEAN bTerminate;
} DEVICE_EXTENSION, *PDEVICE_EXTENSION;

typedef struct _MESSAGE_ENTRY {
	LIST_ENTRY ListEntry;
	PMESSAGE_FORM pMessageForm;
} MESSAGE_ENTRY, *PMESSAGE_ENTRY;
#pragma pack()

WCHAR nameBuffer[] = L"\\Device\\MemoryReviewer";
WCHAR linkBuffer[] = L"\\DosDevices\\MemoryReviewer";
PDEVICE_OBJECT pMyDevice = NULL;

BOOLEAN isStartedUserCommunicationThread = FALSE;

VOID UnLoad(PDRIVER_OBJECT pDriverObject) {
	UNICODE_STRING linkName;
	PDEVICE_EXTENSION pExtension = (PDEVICE_EXTENSION)(pDriverObject->DeviceObject->DeviceExtension);
	KIRQL kIrql;
	PMESSAGE_ENTRY pMessageEntry = NULL;
	UCHAR dbgPrefix[] = "::: IN UNLOAD ROUTINE : ";

	// Terminate the Kernel Communication Thread.
	pExtension->bTerminate = TRUE;
	KeReleaseSemaphore(&(pExtension->CommunicationThreadSemaphore), 0, 1, FALSE);	
	KeSetEvent(&(pExtension->PendingIRPEvent), 0, FALSE);
	KeWaitForSingleObject(pExtension->pCommunicationThread, Executive, KernelMode, FALSE, NULL);
	DbgPrintEx(101, 0, "%sTerminate my Communication Thread...\n", dbgPrefix);

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

	// Delete the DEVICE_OBJECT.
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
	PDEVICE_EXTENSION pExtension = (PDEVICE_EXTENSION)(pDeviceObject->DeviceExtension);
	
	// Cancel the Global Cancel Lock
	IoReleaseCancelSpinLock(pIrp->CancelIrql);

	KeAcquireSpinLock(&(pExtension->PendingIRPLock), &kIrql);	
	if (pExtension->PendingIRP) {
		pExtension->PendingIRP = NULL;

		pIrp->IoStatus.Status = STATUS_CANCELLED;
		pIrp->IoStatus.Information = 0;
	
		KeReleaseSpinLock(&(pExtension->PendingIRPLock), kIrql);
		IoCompleteRequest(pIrp, IO_NO_INCREMENT);

		DbgPrintEx(101, 0, "%sA Pending IRP is Cancelled...\n", dbgPrefix);
	}

	return;
}

BOOLEAN MessageMaker(ULONG messageType, PVOID pMessage, ULONG messageLength) {
	PMESSAGE_ENTRY pMessageEntry = NULL;
	UCHAR dbgPrefix[] = "::: IN Message Maker : ";
	PDEVICE_EXTENSION pExtension = (PDEVICE_EXTENSION)(pMyDevice->DeviceExtension);

	if (pMessage && (messageLength <= 1020)) {
		pMessageEntry = ExAllocatePool(NonPagedPool, sizeof(MESSAGE_ENTRY));
		if (pMessageEntry) {
			RtlZeroMemory(pMessageEntry, sizeof(MESSAGE_ENTRY));
			pMessageEntry->pMessageForm = ExAllocatePool(NonPagedPool, sizeof(MESSAGE_FORM));
			if (pMessageEntry->pMessageForm) {
				RtlZeroMemory(pMessageEntry->pMessageForm, sizeof(MESSAGE_FORM));

				pMessageEntry->pMessageForm->Type = messageType;
				RtlCopyMemory((pMessageEntry->pMessageForm->Message), pMessage, messageLength);

				// Queuing...
				__try {
					ExInterlockedInsertTailList(&(pExtension->MessageListHead), &(pMessageEntry->ListEntry), &(pExtension->MessageLock));
					DbgPrintEx(101, 0, "%sA Message have been queued...\n", dbgPrefix);

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
		
		DbgPrintEx(101, 0, "%sFailed to Allocate the Pool for Message", dbgPrefix);
		return FALSE;
	}
	
}

// It's only for test....
VOID TestForMessageQueuing(ULONG count) {
	WCHAR message[] = L"It's for test.";

	while(count--) {
		if (!MessageMaker(count, message, sizeof(message)))
			break;
	}

	DbgPrintEx(101, 0, " ::: Test Messgae : %ws[%d]\n", message, sizeof(message));
}

NTSTATUS DispatchRead(PDEVICE_OBJECT pDeviceObject, PIRP pIrp) {
	PUCHAR pBuffer = NULL;
	KIRQL kIrql;
	PDEVICE_EXTENSION pExtension = (PDEVICE_EXTENSION)(pDeviceObject->DeviceExtension);
	PVOID pOldCancelRoutine = NULL;
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
				*(PULONG)pBuffer = INITIALIZE_COMMUNICATION;
				RtlCopyMemory(pBuffer + 4, L"HI, I Received your First message...", 72);
				isStartedUserCommunicationThread = TRUE;

				// For Test...
				TestForMessageQueuing(10);

				pIrp->IoStatus.Information = sizeof(MESSAGE_FORM);
				pIrp->IoStatus.Status = STATUS_SUCCESS;
				IoCompleteRequest(pIrp, IO_NO_INCREMENT);

				return STATUS_SUCCESS;
			}
			else {
				KeAcquireSpinLock(&(pExtension->PendingIRPLock), &kIrql);
				if (pExtension->PendingIRP) {
					// A Pending IRP is already exist...
					//	-> Maybe, This case will not be occurred.
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
								// In this case, The Pending IRP will be cancelled in my Cancel Routine...
							}
						}
						else {
							// Pending...
							DbgPrintEx(101, 0, "%sA Pending IRP is occurred...\n", dbgPrefix);
							KeSetEvent(&(pExtension->PendingIRPEvent), 0, FALSE);
						}
					}
					KeReleaseSpinLock(&(pExtension->PendingIRPLock), kIrql);
					return STATUS_PENDING;
				}
			}
		}
	}

	DbgPrintEx(101, 0, "%sThe MDL of the IRP is corrupted...\n", dbgPrefix);
	pIrp->IoStatus.Information = 0;
	pIrp->IoStatus.Status = STATUS_UNSUCCESSFUL;
	IoCompleteRequest(pIrp, IO_NO_INCREMENT);

	return STATUS_UNSUCCESSFUL;
}


// INPUT BUFFER : MDL, OUTPUT BUFFER : SystemBuffer
NTSTATUS DispatchControl(PDEVICE_OBJECT pDeviceObject, PIRP pIrp) {
	PIO_STACK_LOCATION irpStack = NULL;
	NTSTATUS ntStatus = STATUS_UNSUCCESSFUL;
	UCHAR dbgPrefix[] = "::: IN CONTROL Dispatcher : ";
	
	irpStack = IoGetCurrentIrpStackLocation(pIrp);
	if (irpStack) {
		DbgPrintEx(101, 0, "%s   %08X", dbgPrefix, (ULONG)(((irpStack->Parameters.DeviceIoControl.IoControlCode) >> 2) & 0xFFF));
		switch (((irpStack->Parameters.DeviceIoControl.IoControlCode) >> 2) & 0xFFF) {
		case SIN_TERMINATE_USER_THREAD:
			if (isStartedUserCommunicationThread) {
				isStartedUserCommunicationThread = FALSE;
				ntStatus = STATUS_SUCCESS;
				DbgPrintEx(101, 0, "%sMarked the TERMINATE_USER_THREAD...\n", dbgPrefix);
			}

			irpStack->Parameters.DeviceIoControl.OutputBufferLength = 0;
			break;
		default:
			irpStack->Parameters.DeviceIoControl.OutputBufferLength = 0;
			break;
		}

	}

	//pIrp->IoStatus.Status = ntStatus;	-> This 'pIrp->IoStatus' Field is not associated with the IRP_MJ_DEVICE_CONTROL.
	IoCompleteRequest(pIrp, IO_NO_INCREMENT);
	return ntStatus;
}


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
								RtlCopyMemory(MmGetSystemAddressForMdl(pIrp->MdlAddress), (pMessageEntry->pMessageForm), sizeof(MESSAGE_FORM));
								ExFreePool(pMessageEntry->pMessageForm);
								ExFreePool(pMessageEntry);

								pIrp->IoStatus.Status = STATUS_SUCCESS;
								pIrp->IoStatus.Information = sizeof(MESSAGE_FORM);

								IoCompleteRequest(pIrp, IO_NO_INCREMENT);
								break;
							}
							else {
								// In this Case, Jut Continue this Loop...
								DbgPrintEx(101, 0, "%sThe MDL of the Pending IRP is corrupted...\n");
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