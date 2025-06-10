using TinyCsvParser.Mapping;

namespace ChinesePhraseGenerator;

public class CharsMapping : CsvMapping<ChineseChars> {
	public CharsMapping() : base() {
		MapProperty(0, x => x.Rank);
		MapProperty(1, x => x.Char);
	}
}
