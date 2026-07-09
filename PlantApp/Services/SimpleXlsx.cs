using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace PlantApp.Services;

/// <summary>
/// Minimal generator för Office Open XML (.xlsx) utan externa beroenden. En xlsx-fil är
/// egentligen en zip med XML-delar; här skapas ett enda kalkylblad med en fet rubrikrad
/// och celler som antingen är text eller tal. Räcker för enkla tabellexporter.
/// </summary>
public static class SimpleXlsx
{
	/// <summary>En cell i kalkylbladet: antingen text, ett tal, eller tom.</summary>
	public readonly record struct Cell(string? Text, double? Number)
	{
		public static Cell Str(string? text) => new(text ?? string.Empty, null);
		public static Cell Num(double value) => new(null, value);
		public static Cell Empty => new(null, null);
	}

	/// <summary>Ett kalkylblad (flik): namn, rubriker och datarader.</summary>
	public sealed record Sheet(
		string Name,
		IReadOnlyList<string> Headers,
		IReadOnlyList<IReadOnlyList<Cell>> Rows);

	/// <summary>
	/// Skapar en xlsx-fil (som byte-array) med ett kalkylblad. Första raden är rubriker i
	/// fet stil, följt av dataraderna.
	/// </summary>
	public static byte[] Create(
		string sheetName,
		IReadOnlyList<string> headers,
		IReadOnlyList<IReadOnlyList<Cell>> rows)
		=> Create(new[] { new Sheet(sheetName, headers, rows) });

	/// <summary>
	/// Skapar en xlsx-fil (som byte-array) med ett eller flera kalkylblad (flikar). Varje
	/// blad har en rubrikrad i fet stil följt av sina datarader.
	/// </summary>
	public static byte[] Create(IReadOnlyList<Sheet> sheets)
	{
		if (sheets.Count == 0)
		{
			throw new ArgumentException("Minst ett kalkylblad krävs.", nameof(sheets));
		}

		using var ms = new MemoryStream();
		using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
		{
			WriteEntry(zip, "[Content_Types].xml", ContentTypesXml(sheets.Count));
			WriteEntry(zip, "_rels/.rels", RootRelsXml);
			WriteEntry(zip, "xl/workbook.xml", WorkbookXml(sheets));
			WriteEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml(sheets.Count));
			WriteEntry(zip, "xl/styles.xml", StylesXml);

			for (var i = 0; i < sheets.Count; i++)
			{
				WriteEntry(zip, $"xl/worksheets/sheet{i + 1}.xml",
					BuildSheet(sheets[i].Headers, sheets[i].Rows));
			}
		}

		return ms.ToArray();
	}

	private static void WriteEntry(ZipArchive zip, string path, string content)
	{
		var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
		using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
		writer.Write(content);
	}

	private static string BuildSheet(
		IReadOnlyList<string> headers,
		IReadOnlyList<IReadOnlyList<Cell>> rows)
	{
		var sb = new StringBuilder();
		sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
		sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

		// Rubrikrad – fet stil (s="1").
		sb.Append("<row r=\"1\">");
		for (var c = 0; c < headers.Count; c++)
		{
			AppendTextCell(sb, ColumnName(c) + "1", headers[c], styleIndex: 1);
		}
		sb.Append("</row>");

		for (var r = 0; r < rows.Count; r++)
		{
			var rowNr = r + 2;
			sb.Append("<row r=\"").Append(rowNr).Append("\">");

			var row = rows[r];
			for (var c = 0; c < row.Count; c++)
			{
				var reference = ColumnName(c) + rowNr.ToString(CultureInfo.InvariantCulture);
				var cell = row[c];

				if (cell.Number is { } num)
				{
					sb.Append("<c r=\"").Append(reference).Append("\"><v>")
					  .Append(num.ToString(CultureInfo.InvariantCulture))
					  .Append("</v></c>");
				}
				else if (!string.IsNullOrEmpty(cell.Text))
				{
					AppendTextCell(sb, reference, cell.Text, styleIndex: 0);
				}
				// Tom cell hoppas över helt.
			}

			sb.Append("</row>");
		}

		sb.Append("</sheetData></worksheet>");
		return sb.ToString();
	}

	private static void AppendTextCell(StringBuilder sb, string reference, string text, int styleIndex)
	{
		sb.Append("<c r=\"").Append(reference).Append('"');
		if (styleIndex > 0)
		{
			sb.Append(" s=\"").Append(styleIndex).Append('"');
		}
		sb.Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
		  .Append(Escape(text))
		  .Append("</t></is></c>");
	}

	/// <summary>Översätter ett 0-baserat kolumnindex till Excel-notation (A, B, …, AA).</summary>
	private static string ColumnName(int zeroBasedIndex)
	{
		var name = string.Empty;
		var i = zeroBasedIndex;
		do
		{
			name = (char)('A' + i % 26) + name;
			i = i / 26 - 1;
		}
		while (i >= 0);
		return name;
	}

	private static string Escape(string value) => value
		.Replace("&", "&amp;")
		.Replace("<", "&lt;")
		.Replace(">", "&gt;");

	private const string ContentTypesHeader =
		"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
		"<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
		"<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
		"<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
		"<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
		"<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>";

	private static string ContentTypesXml(int sheetCount)
	{
		var sb = new StringBuilder(ContentTypesHeader);
		for (var i = 1; i <= sheetCount; i++)
		{
			sb.Append("<Override PartName=\"/xl/worksheets/sheet").Append(i)
			  .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
		}
		sb.Append("</Types>");
		return sb.ToString();
	}

	private const string RootRelsXml =
		"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
		"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
		"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
		"</Relationships>";

	private static string WorkbookRelsXml(int sheetCount)
	{
		var sb = new StringBuilder();
		sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
		sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
		for (var i = 1; i <= sheetCount; i++)
		{
			sb.Append("<Relationship Id=\"rId").Append(i)
			  .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet")
			  .Append(i).Append(".xml\"/>");
		}
		// Styles-relationen får ett id efter bladen.
		sb.Append("<Relationship Id=\"rId").Append(sheetCount + 1)
		  .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
		sb.Append("</Relationships>");
		return sb.ToString();
	}

	private const string StylesXml =
		"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
		"<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
		"<fonts count=\"2\">" +
		"<font><sz val=\"11\"/><color rgb=\"FF000000\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
		"<font><b/><sz val=\"11\"/><color rgb=\"FF000000\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
		"</fonts>" +
		"<fills count=\"2\">" +
		"<fill><patternFill patternType=\"none\"/></fill>" +
		"<fill><patternFill patternType=\"gray125\"/></fill>" +
		"</fills>" +
		"<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
		"<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
		"<cellXfs count=\"2\">" +
		"<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
		"<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
		"</cellXfs>" +
		"<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
		"</styleSheet>";

	private static string WorkbookXml(IReadOnlyList<Sheet> sheets)
	{
		var sb = new StringBuilder();
		sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
		sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ");
		sb.Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
		sb.Append("<sheets>");

		for (var i = 0; i < sheets.Count; i++)
		{
			var namn = sheets[i].Name;
			if (namn.Length > 31)
			{
				namn = namn[..31];
			}

			sb.Append("<sheet name=\"").Append(Escape(namn))
			  .Append("\" sheetId=\"").Append(i + 1)
			  .Append("\" r:id=\"rId").Append(i + 1).Append("\"/>");
		}

		sb.Append("</sheets></workbook>");
		return sb.ToString();
	}
}
