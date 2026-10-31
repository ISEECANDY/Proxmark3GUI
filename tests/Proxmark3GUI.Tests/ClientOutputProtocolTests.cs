using Microsoft.VisualStudio.TestTools.UnitTesting;
using Proxmark3GUI.Services;

namespace Proxmark3GUI.Tests;

[TestClass]
public sealed class ClientOutputProtocolTests
{
    [TestMethod]
    public void TryNormalizeLine_ReturnsFalseForNullOrWhitespace()
    {
        Assert.IsFalse(ClientOutputProtocol.TryNormalizeLine(null, out var nullLine));
        Assert.AreEqual(string.Empty, nullLine);

        Assert.IsFalse(ClientOutputProtocol.TryNormalizeLine("  \t ", out var whitespaceLine));
        Assert.AreEqual(string.Empty, whitespaceLine);
    }

    [TestMethod]
    public void TryNormalizeLine_RemovesAnsiEscapeSequencesAndTrailingWhitespace()
    {
        var result = ClientOutputProtocol.TryNormalizeLine("\u001b[32m[+] Device ready\u001b[0m   ", out var line);

        Assert.IsTrue(result);
        Assert.AreEqual("[+] Device ready", line);
    }

    [TestMethod]
    public void IsCompletionMarker_RecognizesExactMarkerWithSurroundingWhitespace()
    {
        Assert.IsTrue(ClientOutputProtocol.IsCompletionMarker("  " + ClientOutputProtocol.CompletionMarker + "  "));
    }

    [TestMethod]
    public void IsCompletionMarker_DoesNotMatchOrdinaryOutputOrPartialMarker()
    {
        Assert.IsFalse(ClientOutputProtocol.IsCompletionMarker("command complete"));
        Assert.IsFalse(ClientOutputProtocol.IsCompletionMarker("__PM3GUI_COMMAND_COMPLETE__ extra"));
        Assert.IsFalse(ClientOutputProtocol.IsCompletionMarker("__PM3GUI_COMMAND_COMPLET"));
    }

    [TestMethod]
    public void TryNormalizeLine_PreservesMeaningfulInteriorWhitespace()
    {
        var result = ClientOutputProtocol.TryNormalizeLine("  HF 14a   ", out var line);

        Assert.IsTrue(result);
        Assert.AreEqual("  HF 14a", line);
    }
}
