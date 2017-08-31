#pragma once

#include "stdafx.h"

#define BUFFER_LEN 8192

class MyWindow {

	bool _saveIcon(HICON hIcon, int nColorBits, const TCHAR* szPath);

public:

	char	exe_path[MAX_PATH + 1];
	char	window_name[MAX_PATH + 1];
	HWND	hwnd;
	DWORD	process_id = 0;
	BOOL	iconExists = FALSE;
	HICON	icon[1];
	TCHAR	icon_path[MAX_PATH];

	char*	toString(int& size);	
	bool	saveIcon();
	
};