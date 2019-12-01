Imports System
Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading

Class MainVB
    Public Shared Sub Main()

        Dim ip As String = "127.0.0.1"
        Dim port As UShort = 8888
        Dim server As ServerWebSocket = New ServerWebSocket(ip, port)
        server.Start()
        Console.WriteLine("Server has started on {0}:{1}", ip, port)


        Console.ReadLine()

        server.BroadCast(Encoding.UTF8.GetBytes("Gửi thông điệp đến tất cả client"))
    End Sub
End Class

Public Class ServerWebSocket
    Private Clients As List(Of TcpClient) = New List(Of TcpClient)
    Private Ip As String
    Private Port As UShort
    Public Sub Start()
        Dim server = New TcpListener(IPAddress.Parse(Ip), Port)
        server.Start()

        Dim thread As Thread = New Thread(Sub()
                                              While True
                                                  Dim client As TcpClient = server.AcceptTcpClient()
                                                  Dim session As Thread = New Thread(
                                                      Async Sub()
                                                          Connect(client)
                                                          Await Receive(client)
                                                      End Sub
                                                      )
                                                  session.Start()
                                              End While
                                          End Sub)
        thread.Start()
    End Sub
    Public Sub New(ByVal ip As String, ByVal port As UShort)
        Me.Ip = ip
        Me.Port = port
    End Sub

    Private Function Read(ByRef stream As NetworkStream) As Byte()
        Dim data As Byte() = New Byte(1023) {}

        Using memoryStream As MemoryStream = New MemoryStream()

            Do
                stream.Read(data)
                memoryStream.Write(data)
            Loop While stream.DataAvailable

            Return memoryStream.ToArray()
        End Using
    End Function

    Private Sub Connect(ByVal client As TcpClient)

        Dim stream As NetworkStream = client.GetStream()

        Dim isConnected As Boolean = False
        While isConnected <> True

            Dim bytes As Byte() = Read(stream)

            Dim s As String = Encoding.UTF8.GetString(bytes)

            If Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase) Then
                HandShaking(stream, s)
                Clients.Add(client)
                isConnected = True
                Console.WriteLine("Client connected.")
            End If

        End While
    End Sub

    Private Async Function Receive(ByVal client As TcpClient) As Task(Of String)

        Dim stream As NetworkStream = client.GetStream()
        While True

            While client.Available < 3

                Await Task.Delay(100)
                GC.Collect()

            End While

            Dim bytes As Byte() = Read(stream)

            Dim value As Integer = bytes(0)
            Dim bitArray As BitArray = New BitArray(8)

            For c As Integer = 0 To 7 Step 1
                If value - (2 ^ (7 - c)) >= 0 Then
                    bitArray.Item(c) = True
                    value -= (2 ^ (7 - c))
                Else
                    bitArray.Item(c) = False
                End If
            Next

            Dim FRRR_OPCODE As String = ""
            For Each bit As Boolean In bitArray
                If bit Then
                    FRRR_OPCODE &= "1"
                Else
                    FRRR_OPCODE &= "0"
                End If
            Next
            Dim opCode As Integer = Convert.ToInt32(FRRR_OPCODE.Substring(4, 4), 2)
            Select Case opCode
                Case Is = 1
                    'Text Data Sent From Client
                    Send(client.GetStream(), Decode(bytes))
                Case Else '// Improper opCode.. disconnect the client 
                    Clients.Remove(client)
            End Select
        End While

    End Function

    Sub BroadCast(ByRef data As Byte())

        For Each client In Clients
            Send(client.GetStream, data)
        Next

    End Sub
    Private Sub HandShaking(ByRef stream As NetworkStream, ByRef s As String)

        Dim swk As String = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups(1).Value.Trim()
        Dim swka As String = swk & "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
        Dim swkaSha1 As Byte() = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka))
        Dim swkaSha1Base64 As String = Convert.ToBase64String(swkaSha1)
        Dim response As Byte() = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" & vbCrLf & "Connection: Upgrade" & vbCrLf & "Upgrade: websocket" & vbCrLf & "Sec-WebSocket-Accept: " & swkaSha1Base64 & vbCrLf & vbCrLf)
        stream.Write(response, 0, response.Length)

    End Sub
    Private Sub Send(ByRef stream As NetworkStream, ByRef data As Byte())

        Dim send As Byte() = New Byte() {129}
        Dim actualLength As Byte()

        If data.Length <= 125 Then

            actualLength = New Byte() {data.Length}

        Else
            Dim PayLoadLength As Byte()
            If data.Length <= 65535 Then

                PayLoadLength = New Byte() {126}
                Dim Length As UShort = data.Length
                actualLength = BitConverter.GetBytes(Length)

            Else

                PayLoadLength = New Byte() {127}
                Dim Length As ULong = data.LongLength
                actualLength = BitConverter.GetBytes(Length)

            End If
            Array.Reverse(actualLength)
            send = send.Concat(PayLoadLength).ToArray()
        End If

        send = send.Concat(actualLength).ToArray()
        send = send.Concat(data).ToArray()
        stream.Write(send)
    End Sub

    Private Function Decode(ByRef bytes As Byte()) As Byte()

        Dim mask As Boolean = (bytes(1) And &B10000000) <> 0
        Dim msglen As Integer = bytes(1) - 128, offset As Integer = 2
        Dim decoded As Byte() = New Byte() {}

        If msglen = 126 Then
            msglen = BitConverter.ToUInt16(New Byte() {bytes(3), bytes(2)})
            offset = 4
        ElseIf msglen = 127 Then
            msglen = BitConverter.ToUInt64(New Byte() {bytes(9), bytes(8), bytes(7), bytes(6), bytes(5), bytes(4), bytes(3), bytes(2)})
            offset = 10
        End If

        If msglen = 0 Then

        ElseIf mask Then
            ReDim decoded(msglen - 1)
            Dim masks As Byte() = New Byte() {bytes(offset), bytes(offset + 1), bytes(offset + 2), bytes(offset + 3)}
            offset += 4

            For i As Integer = 0 To msglen - 1
                decoded(i) = CByte((bytes(offset + i) Xor masks(i Mod 4)))
            Next

        Else
            Return bytes
        End If

        Return decoded

    End Function

    Private Function Repeat(ByVal value As Byte, ByVal amount As ULong)
        Dim i As Integer
        Dim results As List(Of Byte) = New List(Of Byte)

        For i = 1 To amount
            results.Add(value)
        Next i
        Return results.ToArray()
    End Function
End Class
