using System.Collections.Concurrent;
using System.Timers;

namespace ChinesePhraseGenerator;

public class ConcurrentStreamWriter {
	private StreamWriter Writer { get; }
	private ConcurrentQueue<string> Queue { get; }
	private System.Timers.Timer Timer { get; }

	public ConcurrentStreamWriter(StreamWriter writer) {
		Writer = writer;
		Queue = [];
		Timer = new(TimeSpan.FromMilliseconds(10)) {
			AutoReset = true
		};
		Timer.Elapsed += (s, e) => {
			while(Queue.TryDequeue(out var str)) {
				if (!string.IsNullOrWhiteSpace(str)) {
					Writer.WriteLine(str);
				}
			}
		};
		Timer.Start();
	}

	public void Add(string str) => Queue.Enqueue(str);
}
