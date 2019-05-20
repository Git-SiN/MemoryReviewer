#pragma once

#include "..\Headers\symbols.h"
#define		SIN_DEV_TYPE					0x00008765



////////////////////////////////////////////////////////////////////////////////////
////			METHOD_OUT_DIRECT, FILE_READ_ACCESS | FILE_WRITE_ACCESS			////
////////////////////////////////////////////////////////////////////////////////////
#define		SIN_MR_GET_VADROOT				0x02
#define		SIN_TERMINATE_USER_THREAD		0xFF



#pragma pack(1)

typedef struct _VAD_ROOT_INFO {
	ULONG pEprocess;
	MM_AVL_TABLE avlTable;
} VAD_ROOT_INFO, *PVAD_ROOT_INFO;

typedef struct _MESSAGE_FORM{
	ULONG Type;
	UCHAR Message[1020];
} MESSAGE_FORM, *PMESSAGE_FORM;

#pragma pack()