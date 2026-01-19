using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Net.Sockets;
using System.IO;
using System;


/// <summary>
/// TCP 채팅 클라이언트 역할을 담당한다
/// 서버에 접속하고 메시지를 송수신하며 채팅 UI에 출력한다
/// </summary>
public class Client : MonoBehaviour
{
    // 접속 정보 입력 UI
    //public InputField IPInput, PortInput, NickInput;

    // 서버에 등록할 클라이언트 닉네임
    string clientName;

    // 소켓 연결 여부
    bool socketReady;

    // TCP 소켓과 스트림
    TcpClient socket;
    NetworkStream stream;

    // 송수신용 라이터 리더
    StreamWriter writer;
    StreamReader reader;

    private void Start()
    {
        ConnectToServer();
    }

    /// <summary>
    /// 입력된 IP와 포트로 서버에 접속한다
    /// 이미 연결되어 있다면 아무 동작도 하지 않는다
    /// </summary>
    public void ConnectToServer()
    {
        // 이미 연결되었다면 함수 무시
        if (socketReady) return;

        // 기본 IP와 포트번호 설정
        //string ip = IPInput.text == "" ? "127.0.0.1" : IPInput.text;
        //int port = PortInput.text == "" ? 7777 : int.Parse(PortInput.text);
        string ip = "3.37.215.9";
        int port = 7777;

        // 소켓 생성 및 스트림 구성
        try
        {
            socket = new TcpClient(ip, port);
            stream = socket.GetStream();
            writer = new StreamWriter(stream);
            reader = new StreamReader(stream);
            socketReady = true;
        }
        catch (Exception e)
        {
            // 연결 실패 시 채팅 UI에 에러 출력
            Chat.instance.ShowMessage($"소켓에러 : {e.Message}");
        }
    }

    /// <summary>
    /// 서버로부터 수신 데이터가 있으면 한 줄 단위로 읽어 처리한다
    /// </summary>
    void Update()
    {
        if (socketReady && stream.DataAvailable)
        {
            string data = reader.ReadLine();
            if (data != null)
                OnIncomingData(data);
        }
    }

    /// <summary>
    /// 서버에서 받은 메시지를 처리한다
    /// 서버가 닉네임을 요청하면 닉네임을 만들어 전송한다
    /// 일반 메시지는 채팅 UI에 출력한다
    /// </summary>
    void OnIncomingData(string data)
    {
        // 서버가 닉네임을 요청하는 경우
        if (data == "%NAME")
        {
            // 닉네임 입력이 비어있으면 랜덤 게스트 닉네임을 만든다
            //clientName = NickInput.text == "" ? "Guest" + UnityEngine.Random.Range(1000, 10000) : NickInput.text;
            clientName = "Guest" + UnityEngine.Random.Range(1000, 10000);

            // 서버에 닉네임 등록 메시지 전송
            Send($"&NAME|{clientName}");
            return;
        }

        // 일반 메시지는 채팅 UI에 표시
        Chat.instance.ShowMessage(data);
    }

    /// <summary>
    /// 서버로 문자열 한 줄을 전송한다
    /// 연결되지 않았다면 전송하지 않는다
    /// </summary>
    void Send(string data)
    {
        if (!socketReady) return;

        writer.WriteLine(data);
        writer.Flush();
    }

    /// <summary>
    /// 전송 버튼 또는 입력 확정 동작에서 호출되어 메시지를 서버로 전송한다
    /// 빈 문자열이면 전송하지 않고 입력창을 비운다
    /// </summary>
    public void OnSendButton(InputField SendInput)
    {
        // 에디터 또는 스탠드얼론 환경에서는 Submit 입력이 아닐 경우 전송을 막는다
        // 버튼 클릭만으로 보내고 싶다면 이 조건을 제거해야 한다
#if (UNITY_EDITOR || UNITY_STANDALONE)
        if (!Input.GetButtonDown("Submit")) return;
        SendInput.ActivateInputField();
#endif
        // 공백만 있는 입력은 무시한다
        if (SendInput.text.Trim() == "") return;

        // 입력 내용을 가져오고 입력창은 비운다
        string message = SendInput.text;
        SendInput.text = "";

        // 서버로 전송
        Send(message);
    }

    /// <summary>
    /// 애플리케이션 종료 시 소켓을 정리한다
    /// </summary>
    void OnApplicationQuit()
    {
        CloseSocket();
    }

    /// <summary>
    /// 스트림과 소켓을 닫고 연결 상태를 해제한다
    /// </summary>
    void CloseSocket()
    {
        if (!socketReady) return;

        writer.Close();
        reader.Close();
        socket.Close();
        socketReady = false;
    }
}
