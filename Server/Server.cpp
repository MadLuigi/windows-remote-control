#include "stdafx.h"
#include "Server.h"
#include "MyWindow.h"

#define WM_USER_SHELLICON WM_USER + 1
#define WM_SOCKET WM_USER + 2

#define MAX_LOADSTRING 100

#define DEFAULT_PORT 12058
#define DEFAULT_BUFLEN 8192

// Variabili globali della finestra di questa appicazione
HWND serverWnd;
HINSTANCE hInst;
NOTIFYICONDATA nidApp;
HMENU hPopMenu;
POINT lpClickPoint;
WCHAR szTitle[MAX_LOADSTRING];
WCHAR szWindowClass[MAX_LOADSTRING];

// Definizione della struct che rappresenta un messaggio da inviare al client
typedef struct _SEND_BUFFER {

	std::unique_ptr<CHAR[]> Buffer;
	int BytesSent;
	int BytesToSend;

	_SEND_BUFFER(const CHAR* text, int size)
	{
		u_short length = htons(size);
		BytesSent = 0;
		BytesToSend = size + sizeof(length);
		Buffer = std::make_unique<CHAR[]>(BytesToSend);
		memcpy_s(Buffer.get(), BytesToSend, reinterpret_cast<char*>(&length), sizeof(length));
		memcpy_s(Buffer.get() + sizeof(length), size, text, size);
	}

	_SEND_BUFFER(_SEND_BUFFER&& sb)
	{
		BytesSent = sb.BytesSent;
		BytesToSend = sb.BytesToSend;
		Buffer = std::move(sb.Buffer);
	}

} SEND_BUFFER;

// Variabili globali per la comunicazione via socket:
SOCKET ListenSocket = INVALID_SOCKET;
SOCKET ClientSocket = INVALID_SOCKET;
std::deque<SEND_BUFFER> messagesToSend;
char receiveByteBuffer[DEFAULT_BUFLEN];
char storedByteBuffer[DEFAULT_BUFLEN];
u_short receiveBufferCounter = 0;
u_short bytesToRead = 2;
bool nextRead = true;

// Variabili globali per il monitoraggio delle finestre
bool monitorRunning = true;
HWINEVENTHOOK hWinEventHook = NULL;
std::map<std::pair<HWND, DWORD>, MyWindow> windows;

// Prototipo delle funzioni usate per la creazione e la gestione dell'interfaccia grafica
ATOM MyRegisterClass(HINSTANCE hInstance);
BOOL InitInstance(HINSTANCE, int);
LRESULT CALLBACK WndProc(HWND, UINT, WPARAM, LPARAM);
INT_PTR CALLBACK About(HWND, UINT, WPARAM, LPARAM);

// Prototipi delle funzioni usate per il parsing dei comadi ricevuti e la serializzazione dei comandi da inviare 
int parseCommand(char* command);
int identifyKey(char* key);
bool doCommand(HWND hWnd, int* tasti, int num_tasti);
void keyUp(int key);
void keyDown(int key);
void sendKey(int key);

// Prototipi delle funzioni usate per il monitoraggio delle finestre
BOOL CALLBACK EnumWindowsProc(HWND hWnd, LPARAM lParam);
void CALLBACK WinEventHookProc(
	HWINEVENTHOOK hWinEventHook,
	DWORD event,
	HWND hwnd,
	LONG idObject,
	LONG idChild,
	DWORD dwEventThread,
	DWORD dwmsEventTime);
BOOL IsAltTabWindow(HWND hwnd);

//
//	Entry point dell'applicazione
//
int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                           _In_opt_ HINSTANCE hPrevInstance,
                           _In_ LPWSTR lpCmdLine,
                           _In_ int nCmdShow)
{
	UNREFERENCED_PARAMETER(hPrevInstance);
	UNREFERENCED_PARAMETER(lpCmdLine);

	// Inizializzare le stringhe globali
	LoadStringW(hInstance, IDS_APP_TITLE, szTitle, MAX_LOADSTRING);
	LoadStringW(hInstance, IDC_SERVER, szWindowClass, MAX_LOADSTRING);
	MyRegisterClass(hInstance);

	// Eseguire l'inizializzazione dell'applicazione:
	if (!InitInstance(hInstance, nCmdShow))
	{
		MessageBox(serverWnd,
		           _T("Errore nell'inizializzazione dell'applicazione"),
		           _T("Errore"),
		           MB_ICONERROR);
		return FALSE;
	}

	HACCEL hAccelTable = LoadAccelerators(hInstance, MAKEINTRESOURCE(IDC_SERVER));

	MSG msg;

	// Ciclo di messaggi principale:
	while (GetMessage(&msg, nullptr, 0, 0))
	{
		if (!TranslateAccelerator(msg.hwnd, hAccelTable, &msg))
		{
			TranslateMessage(&msg);
			DispatchMessage(&msg);
		}
	}

	return (int)msg.wParam;
}

//
//	Registra la classe di finestre
//
ATOM MyRegisterClass(HINSTANCE hInstance)
{
	WNDCLASSEXW wcex;

	wcex.cbSize = sizeof(WNDCLASSEX);

	wcex.style = CS_HREDRAW | CS_VREDRAW;
	wcex.lpfnWndProc = WndProc;
	wcex.cbClsExtra = 0;
	wcex.cbWndExtra = 0;
	wcex.hInstance = hInstance;
	wcex.hIcon = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_SERVER));
	wcex.hCursor = LoadCursor(nullptr, IDC_ARROW);
	wcex.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
	wcex.lpszMenuName = MAKEINTRESOURCEW(IDC_SERVER);
	wcex.lpszClassName = szWindowClass;
	wcex.hIconSm = LoadIcon(wcex.hInstance, MAKEINTRESOURCE(IDI_SMALL));

	return RegisterClassExW(&wcex);
}

//
//   Salva l'handle di istanza e crea la finestra principale
//
BOOL InitInstance(HINSTANCE hInstance, int nCmdShow)
{
	hInst = hInstance;

	serverWnd = CreateWindowW(szWindowClass, szTitle, WS_OVERLAPPEDWINDOW,
		CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, nullptr, nullptr, hInstance, nullptr);

	if (!serverWnd) return FALSE;

	HICON hMainIcon = LoadIcon(hInst, (LPCWSTR)MAKEINTRESOURCE(IDI_SERVER));

	nidApp.cbSize = sizeof(NOTIFYICONDATA);
	nidApp.hWnd = (HWND)serverWnd;
	nidApp.uID = IDI_SERVER;
	nidApp.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
	nidApp.hIcon = hMainIcon;
	nidApp.uCallbackMessage = WM_USER_SHELLICON;
	LoadString(hInst, IDS_APPTOOLTIP, nidApp.szTip, MAX_LOADSTRING);
	Shell_NotifyIcon(NIM_ADD, &nidApp);

	return TRUE;
}

//
//  Elabora i messaggi per la finestra principale.
//
//  WM_USER_SHELLICON - disegna il menu dell'applicazione
//  WM_COMMAND - elabora i comandi del menu dell'applicazione
//  WM_CREATE - definisce il socket in ascolto dopo la creazione della finestra principale
//  WM_DESTROY - libera le risorse prima dell'uscita dall'applicazione
//  WM_SOCKET - elabora i messaggi per il socket asincrono in ascolto
//
LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
	switch (message)
	{
	case WM_USER_SHELLICON:
		switch (LOWORD(lParam))
		{
		case WM_RBUTTONDOWN:
			UINT uFlag = MF_BYPOSITION | MF_STRING;
			GetCursorPos(&lpClickPoint);
			hPopMenu = CreatePopupMenu();
			if (monitorRunning)
			{
				InsertMenu(hPopMenu, 0xFFFFFFFF, MF_BYPOSITION | MF_STRING, IDM_INTERROMPI, _T("Interrompi"));
			}
			else
			{
				InsertMenu(hPopMenu, 0xFFFFFFFF, MF_BYPOSITION | MF_STRING, IDM_AVVIA, _T("Avvia"));
			}
			InsertMenu(hPopMenu, 0xFFFFFFFF, MF_SEPARATOR, IDM_SEP, _T("SEP"));
			InsertMenu(hPopMenu, 0xFFFFFFFF, MF_BYPOSITION | MFT_STRING, IDM_EXIT, _T("Esci"));

			SetForegroundWindow(hWnd);
			TrackPopupMenu(hPopMenu, TPM_LEFTALIGN | TPM_LEFTBUTTON | TPM_BOTTOMALIGN, lpClickPoint.x, lpClickPoint.y, 0, hWnd, NULL);

			return TRUE;
		}
		break;

	case WM_COMMAND:
		{
			int wmId = LOWORD(wParam);
			// Analizzare le selezioni di menu:
			switch (wmId)
			{
			case IDM_AVVIA:
				// Avvia la funzione di Monitor
				monitorRunning = true;
				PostMessage(hWnd, WM_SOCKET, ListenSocket, FD_ACCEPT);
				break;

			case IDM_INTERROMPI:
				// Interrompe la funzione di Monitor
				{ // Update della GUI per far si che l'utente non clicchi più volte sui comandi in una fase di stallo
					UINT uFlag = MF_BYPOSITION | MF_STRING | MF_GRAYED; // Tutti i tasti sono resi "non cliccabili"
					hPopMenu = CreatePopupMenu();
					InsertMenu(hPopMenu, 0xFFFFFFFF, uFlag, IDM_INTERROMPI, _T("Attendi...")); // Bottone "Interrompi" diventa "Attendi..."
					InsertMenu(hPopMenu, 0xFFFFFFFF, MF_SEPARATOR, IDM_SEP, _T("SEP"));
					InsertMenu(hPopMenu, 0xFFFFFFFF, uFlag, IDM_EXIT, _T("Esci"));

					SetForegroundWindow(hWnd);
					TrackPopupMenu(hPopMenu, TPM_LEFTALIGN | TPM_LEFTBUTTON | TPM_BOTTOMALIGN, lpClickPoint.x, lpClickPoint.y, 0, hWnd, NULL);
				}

				// Stop alla cattura degli Hook e chiusura del ClientSocket
				if (hWinEventHook) {
					UnhookWinEvent(hWinEventHook);
					hWinEventHook = NULL;
				}
				if (ClientSocket != INVALID_SOCKET)
				{
					shutdown(ClientSocket, SD_SEND);
					closesocket(ClientSocket);
					ClientSocket = INVALID_SOCKET;
					messagesToSend.clear();
				}
				monitorRunning = false;
				break;

			case IDM_EXIT:
				// Termina l'applicazione server
				if (monitorRunning)
				{
					// Update della GUI per far si che l'utente non clicchi più volte sui comandi in una fase di stallo
					UINT uFlag = MF_BYPOSITION | MF_STRING | MF_GRAYED; // Tutti i tasti sono resi "non cliccabili"
					hPopMenu = CreatePopupMenu();
					InsertMenu(hPopMenu, 0xFFFFFFFF, uFlag, IDM_INTERROMPI, _T("Interrompi"));
					InsertMenu(hPopMenu, 0xFFFFFFFF, MF_SEPARATOR, IDM_SEP, _T("SEP"));
					InsertMenu(hPopMenu, 0xFFFFFFFF, uFlag, IDM_EXIT, _T("Attendi...")); // Bottone "Esci" diventa "Attendi..."

					SetForegroundWindow(hWnd);
					TrackPopupMenu(hPopMenu, TPM_LEFTALIGN | TPM_LEFTBUTTON | TPM_BOTTOMALIGN, lpClickPoint.x, lpClickPoint.y, 0, hWnd, NULL);
				}

				// Chiusura dei socket
				if (ClientSocket != INVALID_SOCKET)
				{
					shutdown(ClientSocket, SD_SEND);
					closesocket(ClientSocket);
				}
				if (ListenSocket != INVALID_SOCKET)
					closesocket(ListenSocket);

				DestroyWindow(hWnd);
				break;

			default:
				return DefWindowProc(hWnd, message, wParam, lParam);
			}
		}
		break;

	case WM_CREATE:
		{
			// Crea la cartella temporanea per il trasferimento delle icone
			if(CreateDirectoryW(L"C:\\PDSServerIcons", NULL) == 0 && ERROR_ALREADY_EXISTS != GetLastError())
			{
				MessageBox(hWnd, _T("Impossibile creare una cartella temporanea per le icone"), _T("Attenzione"), MB_ICONWARNING);
			}

			WSADATA WsaDat;
			int nResult = WSAStartup(MAKEWORD(2, 2), &WsaDat);
			if (nResult != 0)
			{
				MessageBox(hWnd,
				           _T("Inizializzazione Winsock fallita"),
				           _T("Errore"),
				           MB_ICONERROR);
				DestroyWindow(hWnd);
				break;
			}

			ListenSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
			if (ListenSocket == INVALID_SOCKET)
			{
				MessageBox(hWnd,
				           _T("Creazione del socket in ascolto fallita"),
				           _T("Errore"),
				           MB_ICONERROR);
				DestroyWindow(hWnd);
				break;
			}

			SOCKADDR_IN SockAddr;
			SockAddr.sin_port = htons(DEFAULT_PORT);
			SockAddr.sin_family = AF_INET;
			SockAddr.sin_addr.s_addr = htonl(INADDR_ANY);

			if (bind(ListenSocket, (LPSOCKADDR)&SockAddr, sizeof(SockAddr)) == SOCKET_ERROR)
			{
				MessageBox(hWnd, _T("Impossibile effettuare il bind del socket"), _T("Errore"), MB_ICONERROR);
				closesocket(ListenSocket);
				DestroyWindow(hWnd);
				break;
			}

			// Socket asincrono
			nResult = WSAAsyncSelect(ListenSocket,
			                         hWnd,
			                         WM_SOCKET,
			                         FD_ACCEPT);
			if (nResult)
			{
				MessageBox(hWnd,
				           _T("Chiamata WSAAsyncSelect fallita"),
				           _T("Errore"),
				           MB_ICONERROR);
				closesocket(ListenSocket);
				DestroyWindow(hWnd);
				break;
			}

			if (listen(ListenSocket, SOMAXCONN) == SOCKET_ERROR)
			{
				MessageBox(hWnd,
				           _T("Chiamata listen fallita"),
				           _T("Errore"),
				           MB_OK);
				closesocket(ListenSocket);
				DestroyWindow(hWnd);
				break;
			}
		}
		break;

	case WM_DESTROY:
		// Rimuovi cartella temporanea delle icone
		RemoveDirectoryW(L"C:\\PDSServerIcons");
		// Disattiva Hook 
		if (hWinEventHook) UnhookWinEvent(hWinEventHook);
		// Pulisci socket
		WSACleanup();
		// Cancella icona dalla tray area
		Shell_NotifyIcon(NIM_DELETE, &nidApp);
		PostQuitMessage(0);
		break;

	case WM_SOCKET:
		{
			switch (WSAGETSELECTEVENT(lParam))
			{
			case FD_READ:
				{
					if (nextRead)
					{
						receiveBufferCounter = 0;
						memset(receiveByteBuffer, 0, DEFAULT_BUFLEN);
						memset(storedByteBuffer, 0, DEFAULT_BUFLEN);
						nextRead = false;
					}

					u_short bytesRead = recv(ClientSocket, receiveByteBuffer, bytesToRead, 0);

					if(bytesRead == SOCKET_ERROR)
					{
						if (WSAGetLastError() != WSAEWOULDBLOCK)
						{
							// Connessione persa con il client
							if (hWinEventHook) {
								UnhookWinEvent(hWinEventHook);
								hWinEventHook = NULL;
							}
							closesocket(ClientSocket);
							ClientSocket = INVALID_SOCKET;
							messagesToSend.clear();
							PostMessage(hWnd, WM_SOCKET, ListenSocket, FD_ACCEPT);
						}
						break;
					} 
					else if (bytesRead == 0)
					{
						// Graceful disconnection
						if (hWinEventHook) {
							UnhookWinEvent(hWinEventHook);
							hWinEventHook = NULL;
						}
						shutdown(ClientSocket, SD_SEND);
						closesocket(ClientSocket);
						ClientSocket = INVALID_SOCKET;
						messagesToSend.clear();
						PostMessage(hWnd, WM_SOCKET, ListenSocket, FD_ACCEPT);
						break;
					}
					else if (bytesRead > 0)
					{
						memcpy_s(storedByteBuffer + receiveBufferCounter, bytesToRead - receiveBufferCounter, receiveByteBuffer, bytesRead);
						receiveBufferCounter += bytesRead;

						if (receiveBufferCounter == bytesToRead)
						{
							if (receiveBufferCounter == 2)
							{
								// Leggi la lunghezza del messaggio: intero senza segno da 2 byte codificato in network byte order (Big Endian)
								bytesToRead = ((storedByteBuffer[0] << 8) & 0xFF00) | (storedByteBuffer[1] & 0xFF);
							}
							else
							{
								parseCommand(storedByteBuffer);
								bytesToRead = 2;
							}
							nextRead = true;
						}
					}		
				}
				break;

			case FD_WRITE:
				while (!messagesToSend.empty())
				{
					char* sendbuf;
					int bufsize;

					sendbuf = messagesToSend.front().Buffer.get() + messagesToSend.front().BytesSent;
					bufsize = messagesToSend.front().BytesToSend - messagesToSend.front().BytesSent;
					int SendBytes = send(ClientSocket, sendbuf, bufsize, 0);

					if (SendBytes == SOCKET_ERROR)
					{
						if (WSAGetLastError() != WSAEWOULDBLOCK)
						{
							// Connessione persa con il client
							if (hWinEventHook) {
								UnhookWinEvent(hWinEventHook);
								hWinEventHook = NULL;
							}
							closesocket(ClientSocket);
							ClientSocket = INVALID_SOCKET;
							messagesToSend.clear();
							PostMessage(hWnd, WM_SOCKET, ListenSocket, FD_ACCEPT);
						}
						break;
					}

					messagesToSend.front().BytesSent += SendBytes;

					if (messagesToSend.front().BytesSent == messagesToSend.front().BytesToSend) {
						messagesToSend.pop_front();
					}
				}
				break;

			case FD_CLOSE:
				// Il client ha chiuso la connessione
				if (hWinEventHook) {
					UnhookWinEvent(hWinEventHook);
					hWinEventHook = NULL;
				}
				closesocket(ClientSocket);
				ClientSocket = INVALID_SOCKET;
				messagesToSend.clear();
				PostMessage(hWnd, WM_SOCKET, ListenSocket, FD_ACCEPT);
				break;

			case FD_ACCEPT:
				// Nuova richiesta di connessione a ListenSocket

				// Se il ClientSocket è già impegnato da un altro client non accetto la connessione
				if (ClientSocket == INVALID_SOCKET && monitorRunning)
				{
					if ((ClientSocket = accept(ListenSocket, NULL, NULL)) == INVALID_SOCKET)
						break;

					messagesToSend.clear();
					windows.clear();

					// Metto in ascolto il main thread sugli eventi READ-WRITE-CLOSE sul ClientSocket
					WSAAsyncSelect(ClientSocket, hWnd, WM_SOCKET, FD_READ | FD_WRITE | FD_CLOSE);

					// Enumerazione delle finestre attive in questo momento
					EnumWindows(EnumWindowsProc, NULL);

					// Finestra in focus
					HWND window_in_focus = GetForegroundWindow();
					MyWindow Gwin_in_focus;

					// Invia le informazioni riguardanti le finestre
					char sendbuffer[DEFAULT_BUFLEN];
					int sendbuffer_size;

					for (auto win_it = windows.begin(); win_it != windows.end(); ++win_it)
					{
						char* tmp_sendvbuf = win_it->second.toString(sendbuffer_size);
						messagesToSend.push_back(SEND_BUFFER(tmp_sendvbuf, sendbuffer_size));
						delete[] tmp_sendvbuf;
						PostMessage(hWnd, WM_SOCKET, ClientSocket, FD_WRITE);

						if (window_in_focus != NULL && window_in_focus == win_it->second.hwnd)
							Gwin_in_focus = win_it->second;
					}

					// Invia le info sulla finestra in focus (se presente)
					if (window_in_focus == NULL) sprintf_s(sendbuffer, "focus\nnullwindow\n");
					else if (Gwin_in_focus.process_id == 0) sprintf_s(sendbuffer, "focus\nwindowsoperatingsystem\n");
					else sprintf_s(sendbuffer, "focus\n%d\n%d", Gwin_in_focus.hwnd, Gwin_in_focus.process_id);

					messagesToSend.push_back(SEND_BUFFER(sendbuffer, strlen(sendbuffer)));
					PostMessage(hWnd, WM_SOCKET, ClientSocket, FD_WRITE);
					
					// Inizializza l'hook per ricevere eventi sulle finestre create/chiuse/cambi focus
					hWinEventHook = SetWinEventHook(EVENT_MIN, EVENT_MAX,
						                       NULL, WinEventHookProc, 0, 0,
						                       WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
				}
				else
				{
					SOCKET toBeClosed = accept(ListenSocket, NULL, NULL);
					shutdown(toBeClosed, SD_SEND);
					closesocket(toBeClosed);
				}
				break;
			}
		}
		break;

	default:
		return DefWindowProc(hWnd, message, wParam, lParam);
	}
	return 0;
}

void CALLBACK WinEventHookProc(
	HWINEVENTHOOK hWinEventHook,
	DWORD event,
	HWND hwnd,
	LONG idObject,
	LONG idChild,
	DWORD dwEventThread,
	DWORD dwmsEventTime)
{
	char sendbuffer[DEFAULT_BUFLEN];
	int sendbuffer_size;

	if (hwnd && idChild == CHILDID_SELF)
	{
		switch (event)
		{
		case EVENT_OBJECT_SHOW:
			if(idObject == OBJID_WINDOW && IsAltTabWindow(hwnd))
			{
				MyWindow wnd;

				wnd.hwnd = hwnd;
				GetWindowThreadProcessId(hwnd, &wnd.process_id);

				// EXE & ICON
				HANDLE processHandle = NULL;
				TCHAR filename[MAX_PATH + 1];
				DWORD size = MAX_PATH;
				SHFILEINFO shfileinfo;

				// processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, wnd.process_id);
				processHandle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, wnd.process_id);
				if (processHandle != NULL)
				{
					// Salvataggio nome applicazione
					if (QueryFullProcessImageNameA(processHandle, 0, wnd.exe_path, &size) == 0)
					{
						// Non è stato possibile ricavare il nome dell'applicazione
						CloseHandle(processHandle);
						break;
					}
					else if (wnd.exe_path[0] == '\0')
					{
						// Questo è un comando del prompt
						CloseHandle(processHandle);
						break;
					}

					Sleep(100);
					GetWindowTextA(hwnd, wnd.window_name, MAX_PATH); // In questo caso salvo il nome visualizzato nella finestra (usato dall'utente Client per discriminare le applicazioni che hanno più finestre grafiche)

					// Salvataggio icona applicazione
					size = MAX_PATH;
					if (QueryFullProcessImageName(processHandle, 0, filename, &size) == 0) 
						wnd.iconExists = FALSE;
					else if (SHGetFileInfo(filename, FILE_ATTRIBUTE_NORMAL, &shfileinfo, sizeof(SHFILEINFO), SHGFI_USEFILEATTRIBUTES | SHGFI_SYSICONINDEX | SHGFI_ICON | SHGFI_LARGEICON) == 0) 
						wnd.iconExists = FALSE;
					else
					{
						wnd.iconExists = TRUE;
						wnd.icon[0] = shfileinfo.hIcon;
					}

					CloseHandle(processHandle);
				}
				else
				{
					// Errore apertura processHandle
					wnd.iconExists = FALSE;
				}

				// Inserimento della nuova finestra nella struttura dati
				windows.insert(std::pair<std::pair<HWND, DWORD>, MyWindow>(std::pair<HWND, DWORD>(wnd.hwnd, wnd.process_id), std::move(wnd)));

				// Invio informazioni sulla nuova finestra creata
				char* tmp_sendvbuf = wnd.toString(sendbuffer_size);
				messagesToSend.push_back(SEND_BUFFER(tmp_sendvbuf, sendbuffer_size));
				delete[] tmp_sendvbuf;
				PostMessage(serverWnd, WM_SOCKET, ClientSocket, FD_WRITE);

				// Setta il focus sulla finesra appena creata
				sprintf_s(sendbuffer, "focus\n%d\n%d", wnd.hwnd, wnd.process_id);
				messagesToSend.push_back(SEND_BUFFER(sendbuffer, strlen(sendbuffer)));
				PostMessage(serverWnd, WM_SOCKET, ClientSocket, FD_WRITE);

			}
			break;

		case EVENT_OBJECT_HIDE:
		case EVENT_OBJECT_DESTROY:
		case EVENT_CONSOLE_END_APPLICATION:
			//if (IsAltTabWindow(hwnd))
			{
				DWORD process_id;
				if (event == EVENT_CONSOLE_END_APPLICATION)
				{
					process_id = idObject;
				}
				else if (idObject == OBJID_WINDOW)
				{
					GetWindowThreadProcessId(hwnd, &process_id);
				} 
				else
					break;

				auto curr_win = windows.find(std::pair<HWND, DWORD>(hwnd, process_id));
				if (curr_win != windows.end())
				{
					sprintf_s(sendbuffer, "closed\n%d %d\n", curr_win->second.hwnd, curr_win->second.process_id);
					messagesToSend.push_back(SEND_BUFFER(sendbuffer, strlen(sendbuffer)));
					PostMessage(serverWnd, WM_SOCKET, ClientSocket, FD_WRITE);
					windows.erase(curr_win);
				}
			}
			break;
		
		case EVENT_SYSTEM_MINIMIZESTART:
		case EVENT_SYSTEM_MINIMIZEEND:
		case EVENT_SYSTEM_FOREGROUND:
			if (idObject == OBJID_WINDOW)
			{
				DWORD process_id;
				HWND window_in_focus = GetForegroundWindow();
				GetWindowThreadProcessId(window_in_focus, &process_id);

				auto curr_win = windows.find(std::pair<HWND, DWORD>(window_in_focus, process_id));
				if (curr_win != windows.end())
				{
					if (curr_win->second.process_id == 0) sprintf_s(sendbuffer, "focus\nwindowsoperatingsystem\n");
					else sprintf_s(sendbuffer, "focus\n%d\n%d", curr_win->second.hwnd, curr_win->second.process_id);

					messagesToSend.push_back(SEND_BUFFER(sendbuffer, strlen(sendbuffer)));
					PostMessage(serverWnd, WM_SOCKET, ClientSocket, FD_WRITE);
				}
				else
				{
					messagesToSend.push_back(SEND_BUFFER("focus\nnullwindow\n", 17));
					PostMessage(serverWnd, WM_SOCKET, ClientSocket, FD_WRITE);
				}
			}
			break;
		}
	}
}

BOOL IsAltTabWindow(HWND hwnd)
{
	TITLEBARINFO ti;
	HWND hwndTry, hwndWalk = NULL;

	if (!IsWindowVisible(hwnd))
		return FALSE;

	hwndTry = GetAncestor(hwnd, GA_ROOTOWNER);
	while (hwndTry != hwndWalk)
	{
		hwndWalk = hwndTry;
		hwndTry = GetLastActivePopup(hwndWalk);
		if (IsWindowVisible(hwndTry))
			break;
	}
	if (hwndWalk != hwnd)
		return FALSE;

	// the following removes some task tray programs and "Program Manager"
	ti.cbSize = sizeof(ti);
	GetTitleBarInfo(hwnd, &ti);
	if (ti.rgstate[0] & STATE_SYSTEM_INVISIBLE)
		return FALSE;

	// Tool windows should not be displayed either, these do not appear in the
	// task bar.
	if (GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TOOLWINDOW)
		return FALSE;

	return TRUE;
}

//
// Funzione di interpretazione ed esecuzione dei comandi
//
int parseCommand(char* command)
{
	if (strncmp(command, "command", 7) == 0)
	{
		// Tokenizza ed esegue il comando
		HWND hwnd;
		char* window_handle;
		DWORD pid;

		unsigned int num_tasti;
		int* tasti;

		char* next_token = NULL;
		char* parsing_res = strtok_s(command, " ", &next_token); // parsing_res == "command"
		if (parsing_res == NULL) return 0;

		window_handle = strtok_s(NULL, " ", &next_token); // window_handle == "<window handle>"
		if (window_handle == NULL) return 0;
		hwnd = (HWND)atoi(window_handle);

		parsing_res = strtok_s(NULL, " ", &next_token); // parsing_res = "<process id>"
		if (parsing_res == NULL) return 0;
		pid = (DWORD)atoi(parsing_res);

		parsing_res = strtok_s(NULL, " ", &next_token); // parsin_res == <numero di tasti premuti>
		if (parsing_res == NULL) return 0;
		num_tasti = atoi(parsing_res);

		tasti = new int[num_tasti];

		for (int i = 0; i < num_tasti; i++)
		{
			parsing_res = strtok_s(NULL, " ", &next_token);
			if (parsing_res == NULL) return 0;

			tasti[i] = identifyKey(parsing_res);

			if (tasti[i] == -1)
			{
				delete[](tasti);
				return 0;
			}
		}

		// Trova la finestra relativa a hwnd e passala alla funzione doCommand
		auto wnd = windows.find(std::pair<HWND, DWORD>(hwnd, pid));
		if (wnd == windows.end())
		{
			delete[](tasti);
			return 0;
		}

		if (!doCommand(wnd->second.hwnd, tasti, num_tasti))
		{
			delete[](tasti);
			return 0;
		}

		delete[](tasti);
		return 1;
	}
	else
		return 0; // Messaggio sconosciuto
}

//
// Ritorna -1 se non è riuscito ad identificare il tasto!
//
int identifyKey(char* key)
{
	int virtual_key = (int)strtol(key, NULL, 16); // Il numero letto è in esadecimale

	if (virtual_key > 0 && virtual_key <= 0xFE)
		return virtual_key;
	else
		return -1;
}

bool doCommand(HWND hWnd, int* tasti, int num_tasti)
{
	SendMessage(hWnd, WM_SYSCOMMAND, SC_HOTKEY, (LPARAM)hWnd);
	SendMessage(hWnd, WM_SYSCOMMAND, SC_RESTORE, (LPARAM)hWnd);

	ShowWindow(hWnd, SW_SHOW);
	SetForegroundWindow(hWnd);
	SetFocus(hWnd);

	std::vector<int> modifiers;
	bool modifierFlag = false;

	for (int i = 0; i < num_tasti; i++)
	{
		if (tasti[i] >= VK_LSHIFT && tasti[i] <= VK_RMENU)
		{
			// E' un modificatore quindi lo tengo premuto
			keyDown(tasti[i]);
			modifierFlag = true;
			modifiers.push_back(tasti[i]);
			continue;
		}

		// Tasto normale, lo premo e lo rilascio
		sendKey(tasti[i]);

		// Se erano stati usati dei modificatori li rilascio
		if (modifierFlag)
		{
			std::for_each(modifiers.begin(), modifiers.end(), [](const int& n) { keyUp(n); });
			modifiers.clear();
			modifierFlag = false;
		}
		Sleep(160);
	}

	return true;
}

void keyDown(int key) 
{
	// Setting the input
	INPUT ip;
	ip.type = INPUT_KEYBOARD;
	ip.ki.wScan = 0;
	ip.ki.time = 0;
	ip.ki.dwExtraInfo = 0;

	// Press i-key
	ip.ki.wVk = key;
	ip.ki.dwFlags = 0; // 0 == press
	SendInput(1, &ip, sizeof(INPUT));
}

void keyUp(int key)
{
	// Setting the input
	INPUT ip;
	ip.type = INPUT_KEYBOARD;
	ip.ki.wScan = 0;
	ip.ki.time = 0;
	ip.ki.dwExtraInfo = 0;

	// Release i-key
	ip.ki.wVk = key;
	ip.ki.dwFlags = KEYEVENTF_KEYUP;
	SendInput(1, &ip, sizeof(INPUT));
}

void sendKey(int key)
{
	INPUT ip[2] = { 0 };
	ip[0].type = ip[1].type = INPUT_KEYBOARD;
	ip[0].ki.wVk = ip[1].ki.wVk = key;
	ip[1].ki.dwFlags = KEYEVENTF_KEYUP;
	SendInput(sizeof(ip) / sizeof(INPUT), ip, sizeof(INPUT));
}

//
// Enumerazione delle finestre
// 
BOOL CALLBACK EnumWindowsProc(HWND hWnd, LPARAM lParam)
{
	if (IsAltTabWindow(hWnd))
	{
		MyWindow wnd;

		wnd.hwnd = hWnd;
		GetWindowThreadProcessId(hWnd, &wnd.process_id);

		// EXE & ICON
		HANDLE processHandle = NULL;
		TCHAR filename[MAX_PATH + 1];
		DWORD size = MAX_PATH;
		SHFILEINFO shfileinfo;

		// processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, wnd.process_id);
		processHandle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, wnd.process_id);
		if (processHandle != NULL)
		{
			// Salvataggio nome applicazione
			if (QueryFullProcessImageNameA(processHandle, 0, wnd.exe_path, &size) == 0)
			{
				// Non è stato possibile ricavare il nome dell'applicazione
				CloseHandle(processHandle);
				return TRUE;
			}
			else if (wnd.exe_path[0] == '\0')
			{
				// Questo è un comando del prompt
				CloseHandle(processHandle);
				return TRUE;
			}

			GetWindowTextA(hWnd, wnd.window_name, MAX_PATH); // In questo caso salvo il nome visualizzato nella finestra (usato dall'utente Client per discriminare le applicazioni che hanno più finestre grafiche)

			// Salvataggio icona applicazione
			size = MAX_PATH;
			if (QueryFullProcessImageName(processHandle, 0, filename, &size) == 0)
				wnd.iconExists = FALSE;
			else if (SHGetFileInfo(filename, FILE_ATTRIBUTE_NORMAL, &shfileinfo, sizeof(SHFILEINFO), SHGFI_USEFILEATTRIBUTES | SHGFI_SYSICONINDEX | SHGFI_ICON | SHGFI_LARGEICON) == 0)
				wnd.iconExists = FALSE;
			else
			{
				wnd.iconExists = TRUE;
				wnd.icon[0] = shfileinfo.hIcon;
			}

			CloseHandle(processHandle);
		}
		else
		{
			// Errore apertura processHandle
			wnd.iconExists = FALSE;
		}

		// Inserimento della nuova finestra nella struttura dati
		windows.insert(std::pair<std::pair<HWND, DWORD>, MyWindow>(std::pair<HWND, DWORD>(wnd.hwnd, wnd.process_id), std::move(wnd)));
		
	}

	return TRUE;
}
