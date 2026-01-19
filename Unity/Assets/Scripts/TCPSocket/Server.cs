using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// TCP 기반 채팅 서버를 실행하는 컴포넌트
/// 유니티 씬에 이 스크립트만 올려두면 실행 시 서버가 자동 시작되며
/// 여러 클라이언트의 접속과 메시지 브로드캐스트를 처리한다
/// </summary>
public class TcpChatServer : MonoBehaviour
{
    [SerializeField] private int defaultPort = 7777;

    private TcpListener listener;
    private bool running;

    private readonly object gate = new object();
    private readonly Queue<TcpClient> pendingAccepts = new Queue<TcpClient>();
    private readonly List<ServerClient> clients = new List<ServerClient>();
    private readonly List<ServerClient> disconnectList = new List<ServerClient>();

    /// <summary>
    /// 백그라운드에서도 서버 루프가 계속 돌도록 설정한다
    /// </summary>
    private void Awake()
    {
        Application.runInBackground = true;
    }

    /// <summary>
    /// 실행 시 포트를 결정하고 서버를 시작한다
    /// 커맨드라인에 port 값이 있으면 그 값을 사용하고 없으면 기본 포트를 사용한다
    /// </summary>
    private void Start()
    {
        int port = ReadPortFromArgs() ?? defaultPort;
        StartServer(port);
    }

    /// <summary>
    /// 서버가 실행 중이면 접속 처리와 메시지 수신 및 브로드캐스트를 매 프레임 처리한다
    /// </summary>
    private void Update()
    {
        if (!running) return;

        // 비동기 Accept에서 들어온 TcpClient를 메인 스레드에서 안전하게 등록한다
        FlushPendingAccepts();

        // 등록된 클라이언트들의 연결 상태와 수신 데이터를 처리한다
        lock (gate)
        {
            disconnectList.Clear();

            for (int i = 0; i < clients.Count; i++)
            {
                var c = clients[i];

                // 연결이 끊긴 클라이언트는 이후 정리 대상으로 모아둔다
                if (!c.IsConnected())
                {
                    disconnectList.Add(c);
                    continue;
                }

                // 읽을 데이터가 있는 경우 한 줄 단위로 읽어 처리한다
                if (c.stream.DataAvailable)
                {
                    string data = c.reader.ReadLine();
                    if (!string.IsNullOrEmpty(data))
                        OnIncomingData(c, data);
                }
            }

            // 끊긴 클라이언트들을 목록에서 제거하고 종료 처리한다
            for (int i = 0; i < disconnectList.Count; i++)
            {
                var dc = disconnectList[i];
                clients.Remove(dc);

                Broadcast($"{dc.clientName} 연결이 끊어졌습니다");
                dc.Close();
            }
        }
    }

    /// <summary>
    /// 애플리케이션 종료 시 소켓을 정리하고 서버를 중단한다
    /// </summary>
    private void OnApplicationQuit()
    {
        StopServer();
    }

    /// <summary>
    /// 지정된 포트로 TCP 리스너를 열고 비동기 접속 대기를 시작한다
    /// </summary>
    private void StartServer(int port)
    {
        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            running = true;

            BeginAccept();
            Debug.Log($"[TcpChatServer] Started on port {port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TcpChatServer] Socket error: {e.Message}");
            running = false;
        }
    }

    /// <summary>
    /// 서버 실행을 중단하고 모든 클라이언트 연결을 닫는다
    /// </summary>
    private void StopServer()
    {
        running = false;

        try { listener?.Stop(); } catch { }

        lock (gate)
        {
            for (int i = 0; i < clients.Count; i++)
                clients[i].Close();
            clients.Clear();
        }

        Debug.Log("[TcpChatServer] Stopped");
    }

    /// <summary>
    /// 새로운 클라이언트 접속을 비동기로 대기한다
    /// </summary>
    private void BeginAccept()
    {
        if (!running || listener == null) return;
        listener.BeginAcceptTcpClient(OnAccept, null);
    }

    /// <summary>
    /// 비동기 Accept가 완료되면 호출되며
    /// 접속된 클라이언트를 큐에 저장한 뒤 다시 다음 접속을 대기한다
    /// </summary>
    private void OnAccept(IAsyncResult ar)
    {
        TcpClient tcp = null;

        try
        {
            if (!running || listener == null) return;
            tcp = listener.EndAcceptTcpClient(ar);
        }
        catch
        {
            // 서버 종료 중에는 예외가 발생할 수 있다
        }
        finally
        {
            if (running) BeginAccept();
        }

        if (tcp == null) return;

        // Accept 콜백은 메인 스레드가 아닐 수 있으므로 큐에 넣고 Update에서 처리한다
        lock (gate)
        {
            pendingAccepts.Enqueue(tcp);
        }
    }

    /// <summary>
    /// 접속 큐에 쌓인 클라이언트를 메인 스레드에서 서버 목록에 등록하고
    /// 닉네임 요청 메시지를 보낸다
    /// </summary>
    private void FlushPendingAccepts()
    {
        lock (gate)
        {
            while (pendingAccepts.Count > 0)
            {
                TcpClient tcp = pendingAccepts.Dequeue();
                var sc = new ServerClient(tcp);

                clients.Add(sc);

                // 클라이언트에게 닉네임을 보내달라고 요청한다
                Send(sc, "%NAME");
            }
        }
    }

    /// <summary>
    /// 특정 클라이언트로부터 받은 데이터를 해석하여
    /// 닉네임 등록 또는 일반 채팅 메시지 브로드캐스트를 수행한다
    /// </summary>
    private void OnIncomingData(ServerClient c, string data)
    {
        // 닉네임 등록 메시지 처리
        if (data.StartsWith("&NAME|"))
        {
            c.clientName = data.Substring("&NAME|".Length);
            Broadcast($"{c.clientName}이 연결되었습니다");
            return;
        }

        // 일반 메시지는 모든 클라이언트에게 전달한다
        Broadcast($"{c.clientName} : {data}");
    }

    /// <summary>
    /// 모든 클라이언트에게 메시지를 전송하고 서버 콘솔에도 로그를 출력한다
    /// </summary>
    private void Broadcast(string data)
    {
        Debug.Log($"[Chat] {data}");

        lock (gate)
        {
            for (int i = 0; i < clients.Count; i++)
                Send(clients[i], data);
        }
    }

    /// <summary>
    /// 특정 클라이언트에게 한 줄 메시지를 전송한다
    /// </summary>
    private void Send(ServerClient c, string data)
    {
        try
        {
            c.writer.WriteLine(data);
            c.writer.Flush();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TcpChatServer] Write error to {c.clientName}: {e.Message}");
        }
    }

    /// <summary>
    /// 커맨드라인 인자에서 port 값을 읽어온다
    /// 예를 들어 -port 7777 형태로 전달되면 해당 포트를 사용한다
    /// </summary>
    private int? ReadPortFromArgs()
    {
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("-port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int p))
                        return p;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    /// <summary>
    /// 서버가 관리하는 클라이언트 세션 정보
    /// 소켓과 스트림 리더 라이터 및 닉네임을 보관한다
    /// </summary>
    private class ServerClient
    {
        public TcpClient tcp;
        public NetworkStream stream;
        public StreamReader reader;
        public StreamWriter writer;
        public string clientName = "Guest";

        /// <summary>
        /// TcpClient로부터 스트림과 리더 라이터를 생성한다
        /// </summary>
        public ServerClient(TcpClient clientSocket)
        {
            tcp = clientSocket;
            stream = tcp.GetStream();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream);
        }

        /// <summary>
        /// 현재 소켓이 연결 상태인지 확인한다
        /// 끊김 여부를 빠르게 판별하기 위해 Poll과 Peek 수신을 사용한다
        /// </summary>
        public bool IsConnected()
        {
            try
            {
                if (tcp == null || tcp.Client == null) return false;
                if (!tcp.Client.Connected) return false;

                // 읽기 가능 상태에서 실제로 읽을 데이터가 없으면 연결 종료로 본다
                if (tcp.Client.Poll(0, SelectMode.SelectRead))
                    return tcp.Client.Receive(new byte[1], SocketFlags.Peek) != 0;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 리더 라이터 소켓을 정리하여 연결을 종료한다
        /// </summary>
        public void Close()
        {
            try { writer?.Close(); } catch { }
            try { reader?.Close(); } catch { }
            try { tcp?.Close(); } catch { }
        }
    }
}
