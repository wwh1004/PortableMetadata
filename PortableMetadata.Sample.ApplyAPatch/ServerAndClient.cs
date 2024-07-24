using System.Net.Sockets;
using System.Net;
using System.Text;

public sealed class MyServer {
	readonly TcpListener listener = new(IPAddress.Loopback, 0);

	public int Start() {
		listener.Start();
		listener.AcceptTcpClientAsync().ContinueWith(async t => {
			using var stream = t.Result.GetStream();
			while (true) {
				var buffer = new byte[4];
				await stream.ReadExactlyAsync(buffer);
				int length = BitConverter.ToInt32(buffer, 0);
				buffer = new byte[length];
				await stream.ReadExactlyAsync(buffer);
				var s = Encoding.UTF8.GetString(buffer);
				Console.WriteLine($"Data from client: {s}");
			}
		}, TaskContinuationOptions.OnlyOnRanToCompletion);
		return ((IPEndPoint)listener.LocalEndpoint).Port;
	}
}

public sealed class MyClient {
	public string GetUserName() {
		return Environment.UserName;
	}

	public void Start(int port) {
		var client = new TcpClient();
		client.ConnectAsync(IPAddress.Loopback, port).ContinueWith(async _ => {
			using var stream = client.GetStream();
			for (int i = 0; i < 5; i++) {
				var name = GetUserName();
				int length = Encoding.UTF8.GetByteCount(name);
				var buffer = BitConverter.GetBytes(length);
				await stream.WriteAsync(buffer);
				buffer = Encoding.UTF8.GetBytes(name);
				await stream.WriteAsync(buffer);
				await Task.Delay(1000);
			}
		}, TaskContinuationOptions.OnlyOnRanToCompletion);
	}
}
