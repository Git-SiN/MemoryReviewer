#pragma once

#include "loader.h"
#pragma comment(lib, "..\\Libs\\loader.lib")

#ifndef __MEMORY_DLL_HEADER__
#define __MEMORY_DLL_HEADER__

#ifdef __CPLUSPLUS
extern "C" {
#endif


#ifdef __DLL_SOURCE_CODE
#define SINFUNC		__declspec(dllexport)
#else
#define SINFUNC		__declspec(dllimport)
#endif
	SINFUNC		BOOLEAN ConnectToKernel();
	SINFUNC		BOOLEAN DisConnect();
	SINFUNC		BOOLEAN ReceiveMessage(PMESSAGE_FORM);
	SINFUNC		VOID WriteMessage(PBOOLEAN, PMESSAGE_FORM);
	SINFUNC		BOOLEAN CancelMyPendingIRPs();
	SINFUNC		BOOLEAN SendControlMessage(USHORT, PMESSAGE_FORM);
#ifdef __CPLUSPLUS
}
#endif
#endif



