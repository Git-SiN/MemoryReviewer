#include <stdio.h>
#include <windows.h>
#include <string.h>
#include <tchar.h>

#include "..\Headers\driverforMR.h"
#define __DLL_SOURCE_CODE
#include "..\Headers\dllforMR.h"

#define SIN_DRIVER_NAME		_T("MemoryReviewer")
#define SIN_DEVICE_NAME		_T("\\\\.\\MemoryReviewer")


TCHAR DRIVER_FULL_NAME[MAX_PATH] = { 0, };
HANDLE hDevice = INVALID_HANDLE_VALUE;
OVERLAPPED readOverlapped;
OVERLAPPED controlOverlapped;


BOOLEAN MakeFullName() {
	ULONG bufferLength = 0;

	bufferLength = GetCurrentDirectory(MAX_PATH * sizeof(TCHAR), DRIVER_FULL_NAME);
	if (bufferLength == 0)
		return FALSE;

	_tcscat_s(DRIVER_FULL_NAME, MAX_PATH - 1, _T("\\"));
#ifdef _DEBUG
	_tcscat_s(DRIVER_FULL_NAME, MAX_PATH - 1, _T("i386\\"));
#endif
	_tcscat_s(DRIVER_FULL_NAME, MAX_PATH - 1, SIN_DRIVER_NAME);
	_tcscat_s(DRIVER_FULL_NAME, MAX_PATH - 1, _T(".sys"));
	return TRUE;
}

BOOLEAN ConnectToKernel() {
	// Check the privilege and Load my driver.
	if (TestPrivileges() && MakeFullName() &&
		ManageDriver(DRIVER_FULL_NAME, MANAGE_DRIVER_INSTALL) &&
		ManageDriver(DRIVER_FULL_NAME, MANAGE_DRIVER_START)) {

		hDevice = CreateFile(SIN_DEVICE_NAME, GENERIC_READ | GENERIC_WRITE, 0, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OVERLAPPED, NULL);
		if (hDevice != INVALID_HANDLE_VALUE) {
			ZeroMemory(&readOverlapped, sizeof(OVERLAPPED));
			readOverlapped.hEvent = CreateEvent(NULL, FALSE, FALSE, NULL);
			if (readOverlapped.hEvent != NULL) {
				ZeroMemory(&controlOverlapped, sizeof(OVERLAPPED));
				controlOverlapped.hEvent = CreateEvent(NULL, FALSE, FALSE, NULL);
				if (controlOverlapped.hEvent != NULL)
					return TRUE;
				else
				{
					CloseHandle(readOverlapped.hEvent);
					readOverlapped.hEvent = NULL;
				}
			}

			OutputDebugStringW(L"Failed the CreateEvent() in the OVERLAPPED Structure...\n");
			DisConnect();
		}
		else
			OutputDebugStringW(L"Can not Get the Handle of my device...\n");
	}
	else
		OutputDebugStringW(L"Failed to Load my driver...\n");

	return FALSE;
}

BOOLEAN CancelMyPendingIRPs() {
	BOOLEAN result;

	// If the Function FAILS, It returns ZERO.
	result = !CancelIoEx(hDevice, NULL);
	if (!result) {
		if (GetLastError() == ERROR_SUCCESS) {
			// IN MSDN : if any Pending IRPs are not found, The result is FALSE & GetLastError() returns ERROR_NOT_FOUND.
			//			  But In Practice, The result is FALSE & GetLastError() returns ERROR_SUCCESS[0x0].
			result = TRUE;
		}
	}
	
	return result;
}

BOOLEAN DisConnect() {
	CancelMyPendingIRPs();

	if (readOverlapped.hEvent != NULL)
		CloseHandle(readOverlapped.hEvent);
	if (controlOverlapped.hEvent != NULL)
		CloseHandle(controlOverlapped.hEvent);
	if(hDevice != INVALID_HANDLE_VALUE)
		CloseHandle(hDevice);

	return ((ManageDriver(DRIVER_FULL_NAME, MANAGE_DRIVER_STOP)) &&
		(ManageDriver(DRIVER_FULL_NAME, MANAGE_DRIVER_REMOVE)));
}


BOOLEAN ReceiveMessage(PMESSAGE_FORM pMessage) {
	BOOLEAN result = FALSE;
	ULONG readLength = 0;
	//WCHAR msg[100];

	if (pMessage) {
		result = ReadFile(hDevice, pMessage, sizeof(MESSAGE_FORM), &readLength, &readOverlapped);
		if (!result) {
			if (GetLastError() == ERROR_IO_PENDING) {
				WaitForSingleObject(readOverlapped.hEvent, INFINITE);
				result = GetOverlappedResult(hDevice, &readOverlapped, &readLength, FALSE);
			}
		}
	}

	// For Debug...
	//ZeroMemory(msg, 200);
	//wsprintfW(msg, L"::: IN DLL : %ws[%d]", result ? L"TRUE" : L"FALSE", length);
	//OutputDebugStringW(msg);

	return result;
}

BOOLEAN SendControlMessage(UCHAR ctlCode, PMESSAGE_FORM pMessage) {
	BOOLEAN result = FALSE;
	ULONG readLength = 0;
	WCHAR msg[100];

	result = DeviceIoControl(hDevice, CTL_CODE(SIN_DEV_TYPE, ctlCode, METHOD_OUT_DIRECT, FILE_READ_ACCESS | FILE_WRITE_ACCESS), pMessage, sizeof(MESSAGE_FORM), pMessage, sizeof(MESSAGE_FORM), &readLength, &controlOverlapped);
	if (!result) {
		if (GetLastError() == ERROR_IO_PENDING) {
			WaitForSingleObject(controlOverlapped.hEvent, INFINITE);
			result = GetOverlappedResult(hDevice, &controlOverlapped, &readLength, FALSE);
		}
	}

	// For Debug...
	ZeroMemory(msg, 200);
	wsprintfW(msg, L"::: IN DLL : %ws[%d]", result ? L"TRUE" : L"FALSE", readLength);
	OutputDebugStringW(msg);

	return result;
}