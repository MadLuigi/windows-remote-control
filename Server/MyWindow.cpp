#pragma once

#include "stdafx.h"
#include "MyWindow.h"

char* MyWindow::toString(int& size) {
	
	char buf[BUFFER_LEN];
	char* icon_buffer = NULL;
	char* res = NULL;

	// Salvataggio della icona nella stringa
	if (iconExists) 
	{ 
		if (!saveIcon()) 
		{
			// Se non è riuscito a salvare l'icona
			sprintf_s(buf, "newwindow\n%s\n%s\n%d\n%d\n-1\n", exe_path, window_name, hwnd, process_id);	// L'icona ha dimensione -1 -> esiste ma non può essere salvata
			size = strlen(buf);
			res = new char[size];
			memcpy_s(res, size, buf, size);
		}
		else 
		{
			DWORD dwNumberOfBytesRead;
			LARGE_INTEGER iconSize;

			HANDLE hFile = CreateFile(
				icon_path,
				GENERIC_READ,
				FILE_SHARE_READ,
				NULL,
				OPEN_EXISTING,
				FILE_ATTRIBUTE_NORMAL,
				NULL);

			if (hFile == INVALID_HANDLE_VALUE) 	// Non invia l'icona
			{
				MessageBox(NULL, _T("Non è stato possibile inviare l'icona."), _T("Errore lettura icona"), MB_OK | MB_ICONERROR);
				sprintf_s(buf, "newwindow\n%s\n%s\n%d\n%d\n-3\n", exe_path, window_name, hwnd, process_id);	// L'icona ha dimensione -3 -> errore lettura icona
				size = strlen(buf);
				res = new char[size];
				memcpy_s(res, size, buf, size);
			}
			else 
			{
				if (!GetFileSizeEx(hFile, &iconSize))
				{
					MessageBox(NULL, _T("Non è stato possibile inviare l'icona."), _T("Errore lettura icona"), MB_OK | MB_ICONERROR);
					sprintf_s(buf, "newwindow\n%s\n%s\n%d\n%d\n-3\n", exe_path, window_name, hwnd, process_id);	// L'icona ha dimensione -3 -> errore lettura icona
					size = strlen(buf);
					res = new char[size];
					memcpy_s(res, size, buf, size);
				}
				else
				{
					icon_buffer = new char[iconSize.QuadPart];

					if (ReadFile(hFile, icon_buffer, iconSize.QuadPart, &dwNumberOfBytesRead, NULL) == FALSE || iconSize.QuadPart != dwNumberOfBytesRead)
					{
						MessageBox(NULL, _T("Non è stato possibile inviare l'icona."), _T("Errore lettura icona"), MB_OK | MB_ICONERROR);
						sprintf_s(buf, "newwindow\n%s\n%s\n%d\n%d\n-3\n", exe_path, window_name, hwnd, process_id);	// L'icona ha dimensione -3 -> errore lettura icona
						size = strlen(buf);
						res = new char[size];
						memcpy_s(res, size, buf, size);
					} 
					else
					{
						sprintf_s(buf, "newwindow\n%s\n%s\n%d\n%d\n%d\n", exe_path, window_name, hwnd, process_id, dwNumberOfBytesRead);

						size_t curr_message_size = strlen(buf);
						size = curr_message_size + dwNumberOfBytesRead;
						res = new char[size];
						memcpy_s(res, size, buf, curr_message_size);
						memcpy_s(res + curr_message_size, size - curr_message_size, icon_buffer, dwNumberOfBytesRead);
					}

					delete[] icon_buffer;
				}
			}

			if (hFile != INVALID_HANDLE_VALUE) CloseHandle(hFile);
			DeleteFile(icon_path);
		}
	}
	else 
	{
		// Se l'icona non esiste
		sprintf_s(buf, "newwindow\n%s\n%s\n%d\n%d\n0\n", exe_path, window_name, hwnd, process_id);	// L'icona ha dimensione 0 -> non esiste
		size = strlen(buf);
		res = new char[size];
		memcpy_s(res, size, buf, size);
	}

	return res;
}

bool MyWindow::saveIcon() {

	if (iconExists) {
		TCHAR icon_path_string[MAX_PATH];
		wsprintf(icon_path_string, L"C:\\PDSServerIcons\\icon%d.ico", (int)hwnd);
		memcpy_s(icon_path, MAX_PATH * sizeof(TCHAR), icon_path_string, MAX_PATH * sizeof(TCHAR));
		if (_saveIcon(icon[0], 32, icon_path)) return true;
		else if (_saveIcon(icon[0], 24, icon_path)) return true;
		else if (_saveIcon(icon[0], 8, icon_path)) return true;
		else if (_saveIcon(icon[0], 4, icon_path)) return true;
		else return false;
	}
	else return false;
}

// SAVE ICON

struct ICONDIRENTRY
{
	UCHAR nWidth;
	UCHAR nHeight;
	UCHAR nNumColorsInPalette; // 0 if no palette
	UCHAR nReserved; // should be 0
	WORD nNumColorPlanes; // 0 or 1
	WORD nBitsPerPixel;
	ULONG nDataLength; // length in bytes
	ULONG nOffset; // offset of BMP or PNG data from beginning of file
};

// Helper class to release GDI object handle when scope ends:
class CGdiHandle
{
public:
	CGdiHandle(HGDIOBJ handle) : m_handle(handle) {};
	~CGdiHandle() { DeleteObject(m_handle); };
private:
	HGDIOBJ m_handle;
};

// Save icon referenced by handle 'hIcon' as file with name 'szPath'.
// The generated ICO file has the color depth specified in 'nColorBits'.
//
bool MyWindow::_saveIcon(HICON hIcon, int nColorBits, const TCHAR* szPath)
{
	ASSERT(nColorBits == 4 || nColorBits == 8 || nColorBits == 24 || nColorBits == 32);

	if (offsetof(ICONDIRENTRY, nOffset) != 12)
	{
		return false;
	}

	CDC dc;
	dc.Attach(::GetDC(NULL)); // ensure that DC is released when function ends

							  // Open file for writing:
	CFile file;
	if (!file.Open(szPath, CFile::modeWrite | CFile::modeCreate))
	{
		return false;
	}

	// Write header:
	UCHAR icoHeader[6] = { 0, 0, 1, 0, 1, 0 }; // ICO file with 1 image
	file.Write(icoHeader, sizeof(icoHeader));

	// Get information about icon:
	ICONINFO iconInfo;
	if (!GetIconInfo(hIcon, &iconInfo)) {
		DWORD debug_error = GetLastError();
	}
	CGdiHandle handle1(iconInfo.hbmColor), handle2(iconInfo.hbmMask); // free bitmaps when function ends
	BITMAPINFO bmInfo = { 0 };
	bmInfo.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
	bmInfo.bmiHeader.biBitCount = 0;    // don't get the color table
	if (!GetDIBits(dc, iconInfo.hbmColor, 0, 0, NULL, &bmInfo, DIB_RGB_COLORS))
	{
		return false;
	}

	// Allocate size of bitmap info header plus space for color table:
	int nBmInfoSize = sizeof(BITMAPINFOHEADER);
	if (nColorBits < 24)
	{
		nBmInfoSize += sizeof(RGBQUAD) * (int)(1 << nColorBits);
	}

	CAutoVectorPtr<UCHAR> bitmapInfo;
	bitmapInfo.Allocate(nBmInfoSize);
	BITMAPINFO* pBmInfo = (BITMAPINFO*)(UCHAR*)bitmapInfo;
	memcpy(pBmInfo, &bmInfo, sizeof(BITMAPINFOHEADER));

	// Get bitmap data:
	ASSERT(bmInfo.bmiHeader.biSizeImage != 0);
	CAutoVectorPtr<UCHAR> bits;
	bits.Allocate(bmInfo.bmiHeader.biSizeImage);
	pBmInfo->bmiHeader.biBitCount = nColorBits;
	pBmInfo->bmiHeader.biCompression = BI_RGB;
	if (!GetDIBits(dc, iconInfo.hbmColor, 0, bmInfo.bmiHeader.biHeight, (UCHAR*)bits, pBmInfo, DIB_RGB_COLORS))
	{
		return false;
	}

	// Get mask data:
	BITMAPINFO maskInfo = { 0 };
	maskInfo.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
	maskInfo.bmiHeader.biBitCount = 0;  // don't get the color table     
	if (!GetDIBits(dc, iconInfo.hbmMask, 0, 0, NULL, &maskInfo, DIB_RGB_COLORS))
	{
		return false;
	}
	ASSERT(maskInfo.bmiHeader.biBitCount == 1);
	CAutoVectorPtr<UCHAR> maskBits;
	maskBits.Allocate(maskInfo.bmiHeader.biSizeImage);
	CAutoVectorPtr<UCHAR> maskInfoBytes;
	maskInfoBytes.Allocate(sizeof(BITMAPINFO) + 2 * sizeof(RGBQUAD));
	BITMAPINFO* pMaskInfo = (BITMAPINFO*)(UCHAR*)maskInfoBytes;
	memcpy(pMaskInfo, &maskInfo, sizeof(maskInfo));
	if (!GetDIBits(dc, iconInfo.hbmMask, 0, maskInfo.bmiHeader.biHeight, (UCHAR*)maskBits, pMaskInfo, DIB_RGB_COLORS))
	{
		return false;
	}

	// Write directory entry:
	ICONDIRENTRY dir;
	dir.nWidth = (UCHAR)pBmInfo->bmiHeader.biWidth;
	dir.nHeight = (UCHAR)pBmInfo->bmiHeader.biHeight;
	dir.nNumColorsInPalette = (nColorBits == 4 ? 16 : 0);
	dir.nReserved = 0;
	dir.nNumColorPlanes = 0;
	dir.nBitsPerPixel = pBmInfo->bmiHeader.biBitCount;
	dir.nDataLength = pBmInfo->bmiHeader.biSizeImage + pMaskInfo->bmiHeader.biSizeImage + nBmInfoSize;
	dir.nOffset = sizeof(dir) + sizeof(icoHeader);
	file.Write(&dir, sizeof(dir));

	// Write DIB header (including color table):
	int nBitsSize = pBmInfo->bmiHeader.biSizeImage;
	pBmInfo->bmiHeader.biHeight *= 2; // because the header is for both image and mask
	pBmInfo->bmiHeader.biCompression = 0;
	pBmInfo->bmiHeader.biSizeImage += pMaskInfo->bmiHeader.biSizeImage; // because the header is for both image and mask
	file.Write(&pBmInfo->bmiHeader, nBmInfoSize);

	// Write image data:
	file.Write((UCHAR*)bits, nBitsSize);

	// Write mask data:
	file.Write((UCHAR*)maskBits, pMaskInfo->bmiHeader.biSizeImage);

	file.Close();

	return true;
}