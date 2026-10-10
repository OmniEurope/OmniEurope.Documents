// SPDX-License-Identifier: EUPL-1.2
using OmniEurope.Documents.Excel;
using static OmniEurope.Documents.Tests.Excel.Formulas.FormulaFixture;

namespace OmniEurope.Documents.Tests.Excel.Formulas;

/// <summary>
/// Each function on the fixture of <see cref="FormulaFixture"/> (A1:A5 = 1 to 5; B1:B4 pomme, poire, Pomme, kiwi;
/// C1 10, C2 "texte", C3 TRUE, C5 2.5; D1 #DIV/0!). Every expected value is worked out by hand from the function's
/// definition in ECMA-376 part 1, §18.17.7.
/// </summary>
public sealed class FormulaFunctionTests
{
    [Theory]
    [InlineData("SUM(A1:A5)", 15.0)]
    [InlineData("SUM(A1:A5,10,\"5\",TRUE)", 31.0)]
    [InlineData("SUM(C1:C5)", 12.5)]
    [InlineData("SUM(C2)", 0.0)]
    [InlineData("PRODUCT(A1:A4)", 24.0)]
    [InlineData("AVERAGE(A1:A5)", 3.0)]
    [InlineData("AVERAGE(C1:C5)", 6.25)]
    [InlineData("MIN(A2:A5,C5)", 2.0)]
    [InlineData("MAX(A1:A5)", 5.0)]
    [InlineData("MAX(B1:B4)", 0.0)]
    [InlineData("COUNT(A1:C5)", 7.0)]
    [InlineData("COUNT(1,\"2\",\"a\",TRUE)", 3.0)]
    [InlineData("COUNTA(A1:C5)", 13.0)]
    [InlineData("COUNTBLANK(B1:C5)", 2.0)]
    [InlineData("COUNTIF(A1:A5,\">2\")", 3.0)]
    [InlineData("COUNTIF(B1:B5,\"pomme\")", 2.0)]
    [InlineData("COUNTIF(B1:B5,\"p*\")", 3.0)]
    [InlineData("COUNTIF(B1:B5,\"?iwi\")", 1.0)]
    [InlineData("COUNTIF(B1:B5,\"<>pomme\")", 3.0)]
    [InlineData("COUNTIF(A1:A5,3)", 1.0)]
    [InlineData("COUNTIF(B1:B5,\"\")", 1.0)]
    [InlineData("COUNTIFS(A1:A5,\">1\",B1:B5,\"p*\")", 2.0)]
    [InlineData("SUMIF(B1:B5,\"pomme\",A1:A5)", 4.0)]
    [InlineData("SUMIF(A1:A5,\">=4\")", 9.0)]
    [InlineData("SUMIFS(A1:A5,B1:B5,\"p*\",A1:A5,\"<3\")", 3.0)]
    [InlineData("AVERAGEIF(B1:B5,\"pomme\",A1:A5)", 2.0)]
    [InlineData("AVERAGEIFS(A1:A5,A1:A5,\">1\",A1:A5,\"<5\")", 3.0)]
    [InlineData("SUMPRODUCT(A1:A3,{2;3;4})", 20.0)]
    [InlineData("ROUND(2.675,2)", 2.68)]
    [InlineData("ROUND(-2.5,0)", -3.0)]
    [InlineData("ROUND(1234.5,-2)", 1200.0)]
    [InlineData("ROUNDUP(3.21,1)", 3.3)]
    [InlineData("ROUNDUP(-3.21,1)", -3.3)]
    [InlineData("ROUNDDOWN(3.29,1)", 3.2)]
    [InlineData("TRUNC(-4.7)", -4.0)]
    [InlineData("INT(-4.3)", -5.0)]
    [InlineData("ABS(-3)", 3.0)]
    [InlineData("SIGN(-0.5)", -1.0)]
    [InlineData("SQRT(16)", 4.0)]
    [InlineData("POWER(2,10)", 1024.0)]
    [InlineData("MOD(-3,2)", 1.0)]
    [InlineData("MOD(3,-2)", -1.0)]
    [InlineData("LOG(1000)", 3.0)]
    [InlineData("LOG(8,2)", 3.0)]
    [InlineData("LOG10(0.01)", -2.0)]
    [InlineData("LN(EXP(2))", 2.0)]
    [InlineData("CEILING(4.2,0.5)", 4.5)]
    [InlineData("FLOOR(4.7,2)", 4.0)]
    [InlineData("CEILING(-4.2,-1)", -5.0)]
    public void Mathematical_functions(string formula, double expected)
    {
        Assert.Equal(expected, (double)Evaluate(formula)!, 12);
    }

    [Theory]
    [InlineData("SUM(A1:D1)", "#DIV/0!")]
    [InlineData("AVERAGE(B1:B4)", "#DIV/0!")]
    [InlineData("SQRT(-1)", "#NUM!")]
    [InlineData("LN(0)", "#NUM!")]
    [InlineData("MOD(1,0)", "#DIV/0!")]
    [InlineData("SUM(\"a\")", "#VALUE!")]
    [InlineData("FLOOR(4,-1)", "#NUM!")]
    [InlineData("SUMIF(A1:A5,1,A1:A2)", "#VALUE!")]
    [InlineData("ROUND(1)", "#VALUE!")]
    public void Mathematical_errors(string formula, string error)
    {
        Assert.Equal(Error(error), Evaluate(formula));
    }

    [Theory]
    [InlineData("IF(A1>0,\"oui\",\"non\")", "oui")]
    [InlineData("IF(A1>5,\"oui\",\"non\")", "non")]
    [InlineData("IF(A1>5,\"oui\")", false)]
    [InlineData("IF(TRUE,1/0,2)", "#DIV/0!")]
    [InlineData("IF(FALSE,1/0,2)", 2.0)]
    [InlineData("IF(A1,,3)", 0.0)]
    [InlineData("IFS(A3=1,\"un\",A3=3,\"trois\")", "trois")]
    [InlineData("IFS(A3=7,\"sept\")", "#N/A")]
    [InlineData("IFERROR(1/0,\"erreur\")", "erreur")]
    [InlineData("IFERROR(4,\"erreur\")", 4.0)]
    [InlineData("IFNA(NA(),0)", 0.0)]
    [InlineData("IFNA(1/0,0)", "#DIV/0!")]
    [InlineData("AND(A1:A5)", true)]
    [InlineData("AND(TRUE,0)", false)]
    [InlineData("OR(FALSE,A1>4,A5>4)", true)]
    [InlineData("XOR(TRUE,TRUE,TRUE)", true)]
    [InlineData("AND(B1:B4)", "#VALUE!")]
    [InlineData("AND(D1)", "#DIV/0!")]
    [InlineData("AND(\"TRUE\",1)", true)]
    [InlineData("AND(\"oui\")", "#VALUE!")]
    [InlineData("OR(C1:C3)", true)]
    [InlineData("AND(B1,TRUE)", true)]
    [InlineData("AND(C4)", "#VALUE!")]
    [InlineData("OR(A1:A2,D1)", "#DIV/0!")]
    [InlineData("NOT(A1=1)", false)]
    [InlineData("CHOOSE(2,\"a\",\"b\",\"c\")", "b")]
    [InlineData("CHOOSE(4,\"a\",\"b\",\"c\")", "#VALUE!")]
    [InlineData("SWITCH(A2,1,\"un\",2,\"deux\",\"autre\")", "deux")]
    [InlineData("SWITCH(A5,1,\"un\",\"autre\")", "autre")]
    [InlineData("SWITCH(A5,1,\"un\")", "#N/A")]
    [InlineData("ISBLANK(C4)", true)]
    [InlineData("ISNUMBER(C2)", false)]
    [InlineData("ISTEXT(B1)", true)]
    [InlineData("ISNONTEXT(A1)", true)]
    [InlineData("ISLOGICAL(C3)", true)]
    [InlineData("ISERROR(D1)", true)]
    [InlineData("ISERR(NA())", false)]
    [InlineData("ISNA(NA())", true)]
    [InlineData("ISEVEN(-4)", true)]
    [InlineData("ISODD(3.9)", true)]
    [InlineData("SUMPRODUCT(--ISNUMBER(C1:C5))", 2.0)]
    public void Logical_and_information_functions(string formula, object expected)
    {
        Assert.Equal(Expected(expected), Evaluate(formula));
    }

    [Theory]
    [InlineData("VLOOKUP(\"kiwi\",B1:C4,1,FALSE)", "kiwi")]
    [InlineData("VLOOKUP(\"POIRE\",B1:C4,2,FALSE)", "texte")]
    [InlineData("VLOOKUP(\"p*\",B1:C4,2,FALSE)", 10.0)]
    [InlineData("VLOOKUP(3.7,A1:C5,3)", true)]
    [InlineData("VLOOKUP(4,A1:C5,3,FALSE)", 0.0)]
    [InlineData("VLOOKUP(9,A1:A5,1,FALSE)", "#N/A")]
    [InlineData("VLOOKUP(0.5,A1:A5,1)", "#N/A")]
    [InlineData("VLOOKUP(1,A1:A5,2,FALSE)", "#REF!")]
    [InlineData("HLOOKUP(10,C1:D1,1,FALSE)", 10.0)]
    [InlineData("HLOOKUP(1,A1:C2,2,FALSE)", 2.0)]
    [InlineData("MATCH(4,A1:A5,0)", 4.0)]
    [InlineData("MATCH(4.5,A1:A5)", 4.0)]
    [InlineData("MATCH(\"KIWI\",B1:B4,0)", 4.0)]
    [InlineData("MATCH(3,{5,4,3,2,1},-1)", 3.0)]
    [InlineData("MATCH(9,A1:A5,0)", "#N/A")]
    [InlineData("INDEX(A1:C5,2,2)", "poire")]
    [InlineData("INDEX(A1:A5,3)", 3.0)]
    [InlineData("INDEX({1,2,3},2)", 2.0)]
    [InlineData("INDEX(A1:C5,4,3)", 0.0)]
    [InlineData("INDEX(A1:A5,9)", "#REF!")]
    [InlineData("INDEX(B1:B5,MATCH(3,A1:A5,0))", "Pomme")]
    [InlineData("XLOOKUP(\"kiwi\",B1:B4,A1:A4)", 4.0)]
    [InlineData("XLOOKUP(\"melon\",B1:B4,A1:A4,\"absent\")", "absent")]
    [InlineData("XLOOKUP(\"melon\",B1:B4,A1:A4)", "#N/A")]
    [InlineData("XLOOKUP(3.5,A1:A5,A1:A5,,1)", 4.0)]
    [InlineData("XLOOKUP(3.5,A1:A5,A1:A5,,-1)", 3.0)]
    [InlineData("XLOOKUP(\"po*\",B1:B4,A1:A4,,2)", 1.0)]
    [InlineData("ROW(C3)", 3.0)]
    [InlineData("COLUMN(C3)", 3.0)]
    [InlineData("ROW()", 100.0)]
    [InlineData("COLUMN()", 26.0)]
    [InlineData("ROWS(A1:C5)", 5.0)]
    [InlineData("COLUMNS(A1:C5)", 3.0)]
    [InlineData("ROWS(A:A)", 1048576.0)]
    [InlineData("COLUMNS({1,2,3,4})", 4.0)]
    public void Lookup_and_reference_functions(string formula, object expected)
    {
        Assert.Equal(Expected(expected), Evaluate(formula));
    }

    [Theory]
    [InlineData("CONCATENATE(B1,\" \",A2)", "pomme 2")]
    [InlineData("CONCAT(A1:A3,\"!\")", "123!")]
    [InlineData("TEXTJOIN(\", \",TRUE,B1:B5)", "pomme, poire, Pomme, kiwi")]
    [InlineData("TEXTJOIN(\"-\",FALSE,C3:C5)", "TRUE--2.5")]
    [InlineData("LEFT(\"Bonjour\",3)", "Bon")]
    [InlineData("LEFT(\"Bonjour\")", "B")]
    [InlineData("RIGHT(\"Bonjour\",4)", "jour")]
    [InlineData("RIGHT(\"ab\",5)", "ab")]
    [InlineData("MID(\"Bonjour\",4,2)", "jo")]
    [InlineData("MID(\"Bonjour\",9,2)", "")]
    [InlineData("MID(\"Bonjour\",0,2)", "#VALUE!")]
    [InlineData("LEN(\"Bonjour\")", 7.0)]
    [InlineData("LEN(A5*100)", 3.0)]
    [InlineData("UPPER(\"Été\")", "ÉTÉ")]
    [InlineData("LOWER(\"ABC\")", "abc")]
    [InlineData("PROPER(\"jean-paul DUPONT\")", "Jean-Paul Dupont")]
    [InlineData("TRIM(\"  deux   espaces  \")", "deux espaces")]
    [InlineData("REPT(\"ab\",3)", "ababab")]
    [InlineData("EXACT(\"a\",\"A\")", false)]
    [InlineData("SUBSTITUTE(\"a-b-c\",\"-\",\"+\")", "a+b+c")]
    [InlineData("SUBSTITUTE(\"a-b-c\",\"-\",\"+\",2)", "a-b+c")]
    [InlineData("SUBSTITUTE(\"a-b-c\",\"-\",\"+\",5)", "a-b-c")]
    [InlineData("REPLACE(\"abcdef\",2,3,\"XY\")", "aXYef")]
    [InlineData("FIND(\"o\",\"Bonjour\")", 2.0)]
    [InlineData("FIND(\"o\",\"Bonjour\",3)", 5.0)]
    [InlineData("FIND(\"O\",\"Bonjour\")", "#VALUE!")]
    [InlineData("SEARCH(\"O\",\"Bonjour\")", 2.0)]
    [InlineData("SEARCH(\"j?u\",\"Bonjour\")", 4.0)]
    [InlineData("SEARCH(\"n*r\",\"Bonjour\")", 3.0)]
    [InlineData("VALUE(\"12.5\")", 12.5)]
    [InlineData("VALUE(\"25%\")", 0.25)]
    [InlineData("VALUE(\"abc\")", "#VALUE!")]
    [InlineData("TEXT(1234.567,\"#,##0.00\")", "1,234.57")]
    [InlineData("TEXT(0.25,\"0%\")", "25%")]
    [InlineData("TEXT(46303,\"yyyy-mm-dd\")", "2026-10-08")]
    [InlineData("CHAR(65)", "A")]
    [InlineData("CODE(\"a\")", 97.0)]
    [InlineData("T(B1)&T(A1)", "pomme")]
    public void Text_functions(string formula, object expected)
    {
        Assert.Equal(Expected(expected), Evaluate(formula));
    }

    [Theory]
    [InlineData("DATE(2026,10,8)", 46303.0)]
    [InlineData("DATE(2026,14,1)", 46419.0)]
    [InlineData("DATE(2026,3,0)", 46081.0)]
    [InlineData("DATE(126,1,1)", 46023.0)]
    [InlineData("DATE(1899,1,1)", 693598.0)]
    [InlineData("DATE(10000,1,1)", "#NUM!")]
    [InlineData("DATE(-1,1,1)", "#NUM!")]
    [InlineData("YEAR(46303)", 2026.0)]
    [InlineData("MONTH(46303)", 10.0)]
    [InlineData("DAY(46303)", 8.0)]
    [InlineData("HOUR(0.75)", 18.0)]
    [InlineData("MINUTE(TIME(10,45,0))", 45.0)]
    [InlineData("SECOND(TIME(0,0,30))", 30.0)]
    [InlineData("TIME(12,0,0)", 0.5)]
    [InlineData("TIME(25,0,0)", 1.0 / 24)]
    [InlineData("WEEKDAY(46303)", 5.0)]
    [InlineData("WEEKDAY(46303,2)", 4.0)]
    [InlineData("WEEKDAY(46303,3)", 3.0)]
    [InlineData("EDATE(DATE(2026,1,31),1)", 46081.0)]
    [InlineData("EOMONTH(DATE(2026,2,10),0)", 46081.0)]
    [InlineData("EOMONTH(DATE(2026,2,10),-1)", 46053.0)]
    [InlineData("DAYS(DATE(2026,12,25),DATE(2026,10,8))", 78.0)]
    [InlineData("DATEDIF(DATE(2000,5,20),DATE(2026,5,19),\"Y\")", 25.0)]
    [InlineData("DATEDIF(DATE(2026,1,31),DATE(2026,3,30),\"M\")", 1.0)]
    [InlineData("DATEDIF(DATE(2026,1,1),DATE(2026,2,1),\"D\")", 31.0)]
    [InlineData("DATEDIF(DATE(2026,2,1),DATE(2026,1,1),\"D\")", "#NUM!")]
    [InlineData("TODAY()", 46303.0)]
    [InlineData("NOW()", 46303.0 + (14.5 / 24))]
    public void Date_functions(string formula, object expected)
    {
        var value = Evaluate(formula);
        if (expected is double number)
        {
            Assert.Equal(number, (double)value!, 9);
        }
        else
        {
            Assert.Equal(Expected(expected), value);
        }
    }

    [Fact]
    public void Dates_count_from_1904_in_a_1904_workbook()
    {
        // 8 October 2026 is 46303 days after 30 December 1899, 1462 fewer after 1 January 1904.
        Assert.Equal(44841.0, Evaluate("DATE(2026,10,8)", date1904: true));
        Assert.Equal(2026.0, Evaluate("YEAR(44841)", date1904: true));
    }

    // Error codes are given as text starting with '#'.
    private static object Expected(object value) => value is string text && text.StartsWith('#') && text.Length > 1 ? Error(text) : value;
}
