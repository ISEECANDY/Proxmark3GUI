using Microsoft.VisualStudio.TestTools.UnitTesting;
using Proxmark3GUI.Services;

namespace Proxmark3GUI.Tests;

[TestClass]
public sealed class DeviceInfoTextParserTests
{
    [TestMethod]
    public void SplitKeyValue_ParsesColonAndEqualsSeparators()
    {
        CollectionAssert.AreEqual(new[] { "Firmware", "  v4.2" }, DeviceInfoTextParser.SplitKeyValue("Firmware:  v4.2"));
        CollectionAssert.AreEqual(new[] { "UID", " 01020304" }, DeviceInfoTextParser.SplitKeyValue("UID= 01020304"));
    }

    [TestMethod]
    public void SplitKeyValue_ParsesDottedStatusSeparator()
    {
        CollectionAssert.AreEqual(new[] { "Flash size", "256KB" }, DeviceInfoTextParser.SplitKeyValue("Flash size........  256KB"));
    }

    [TestMethod]
    public void SplitKeyValue_ParsesRepeatedWhitespaceSeparator()
    {
        CollectionAssert.AreEqual(new[] { "Hardware", "Proxmark3 RDV4" }, DeviceInfoTextParser.SplitKeyValue("Hardware  Proxmark3 RDV4"));
    }

    [TestMethod]
    public void SplitKeyValue_ReturnsEmptyForUnstructuredLine()
    {
        CollectionAssert.AreEqual(Array.Empty<string>(), DeviceInfoTextParser.SplitKeyValue("Proxmark3 client ready"));
    }

    [TestMethod]
    public void SplitKeyValue_RejectsNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() => DeviceInfoTextParser.SplitKeyValue(null!));
    }
}
