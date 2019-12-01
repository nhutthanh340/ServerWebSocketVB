Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading

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
            Dim count As Integer = 0
            Do
                count = stream.Read(data, 0, data.LongLength)
                memoryStream.Write(data, 0, count)
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
                    Dim text As String = "data:image/png;base64,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"
                    Send(client.GetStream(), Encoding.UTF8.GetBytes(text))
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
        stream.Write(send, 0, send.LongLength)
    End Sub

    Private Function Decode(ByRef bytes As Byte()) As Byte()

        Dim mask As Boolean = (bytes(1) And &B10000000) <> 0
        Dim msglen As Integer = bytes(1) - 128, offset As Integer = 2
        Dim decoded As Byte() = New Byte() {}

        If msglen = 126 Then
            msglen = BitConverter.ToUInt16(New Byte() {bytes(3), bytes(2)}, 0)
            offset = 4
        ElseIf msglen = 127 Then
            msglen = BitConverter.ToUInt64(New Byte() {bytes(9), bytes(8), bytes(7), bytes(6), bytes(5), bytes(4), bytes(3), bytes(2)}, 0)
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

Public Class Form1
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim ip As String = "127.0.0.1"
        Dim port As UShort = 8888
        Dim server As ServerWebSocket = New ServerWebSocket(ip, port)
        server.Start()
    End Sub
End Class
