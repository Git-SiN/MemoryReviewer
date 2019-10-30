#pragma once

#include "..\Headers\symbols.h"
#define		SIN_DEV_TYPE					0x00008765



#define		INITIALIZE_COMMUNICATION		0x800
#define		SIN_TERMINATE_USER_THREAD		0x8FF

#define		SIN_URGENT_MESSAGE				0x400

////////////////////////////////////////////////////////////////////////////////////
////			METHOD_OUT_DIRECT, FILE_READ_ACCESS | FILE_WRITE_ACCESS			////
////////////////////////////////////////////////////////////////////////////////////
#define		SIN_MR_GET_VADROOT				0x02

#define		SIN_SET_PRIMARY_OFFSETS			0xF0
#define		SIN_SET_TARGET_OBJECT			0xF1

#define		SIN_GET_BYTE_STREAM				0x40
#define		SIN_GET_KERNEL_OBJECT			0x41

#define		SIN_GET_REQUIRED_OFFSET			0xF0
#define		SIN_RESPONSE_REQUIRED_OFFSET	0x04

#pragma pack(1)

typedef struct _VAD_ROOT_INFO {
	ULONG pEprocess;
	MM_AVL_TABLE avlTable;
} VAD_ROOT_INFO, *PVAD_ROOT_INFO;

typedef struct _REQUIRED_OFFSET {
	ULONG Offset;
	WCHAR ObjectName[128];
	WCHAR FieldName[128];
} REQUIRED_OFFSET, *PREQUIRED_OFFSET;

typedef struct _MESSAGE_FORM {
	USHORT Type;
	USHORT Res;
	ULONG Address;
	ULONG Size;
	union {
		UCHAR bMessage[1024];
		WCHAR uMessage[512];
		REQUIRED_OFFSET Required;
	};
} MESSAGE_FORM, *PMESSAGE_FORM;

#pragma pack()