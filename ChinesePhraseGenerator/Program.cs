using System.Text;
using System.Text.Json;
using System.Text.Json.Schema;
using OllamaSharp;
using OllamaSharp.Models;
using TinyCsvParser;

namespace ChinesePhraseGenerator;
internal class Program {
	static async Task Main(string[] args) {
		Console.OutputEncoding = Encoding.UTF8;
		var parserOpts = new CsvParserOptions(true, ',');
		var charsParser = new CsvParser<ChineseChars>(parserOpts, new CharsMapping());

		var parsedChars = charsParser
				.ReadFromFile("data\\chinese_chars.csv", Encoding.UTF8)
				.Where(cmr => cmr.IsValid)
				.Select(cmr => cmr.Result)
				.ToList();
		parsedChars.Sort((a, b) => a.Rank.CompareTo(b.Rank));
		var processedChars = File.Exists("processed.csv") ? File.ReadAllLines("processed.csv").ToHashSet() : [];
		var top3k = parsedChars.Where(c => c.Rank <= 3000 && !processedChars.Contains(c.Char));

		var uri = new Uri("http://localhost:11434");
		using var httpClient = new HttpClient() {
			BaseAddress = uri,
			Timeout = TimeSpan.FromMinutes(10)
		};
		var ollama = new OllamaApiClient(httpClient);

		using var processedFile = new FileStream("processed.csv", FileMode.Append);
		using var processedStream = new StreamWriter(processedFile) {
			AutoFlush = true
		};
		using var generatedFile = new FileStream("generated.csv", FileMode.Append);
		using var generatedStream = new StreamWriter(generatedFile) {
			AutoFlush = true
		};
		var charsProcessed = new ConcurrentStreamWriter(processedStream);
		var generatedPhrases = new ConcurrentStreamWriter(generatedStream);

		var schema = JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(ChinesePhrases), new() { TreatNullObliviousAsNonNullable = true });
		ParallelOptions options = new() {
			MaxDegreeOfParallelism = 3
		};
		await Parallel.ForEachAsync(top3k, options, async (c, token) => {
			string prompt = $"Generate three short chinese phrases that each include the chinese character \"{c.Char}\"." +
				$" The phrases should be one that a teenager or child would encounter or use in daily life." +
				$" Each phrase needs to be at least 7 to 20 characters long." +
				$" Output JSON, including the pinyin, translation and context if available.";
			var generate = new GenerateRequest {
				Model = "gemma3:latest",
				Stream = false,
				Format = schema,
				Prompt = prompt
			};

			try {
				await foreach (var s in ollama.GenerateAsync(generate, token)) {
					if (s == null) continue;
					var response = JsonSerializer.Deserialize<ChinesePhrases>(s.Response);
					if (response == null) continue;
					foreach (var p in response.phrases) {
						generatedPhrases.Add($"{c.Char}\t{p.phrase}\t{p.pinyin}\t{p.translation}\t{p.context}");
					}
				}
				charsProcessed.Add(c.Char);
			} catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException) {
				Console.WriteLine($"{c.Char} not processed due to timeout");
			} catch {
			}
		});

	}
}
