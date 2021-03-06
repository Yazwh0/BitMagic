using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class LSR
{
    [TestMethod]
    public async Task A()
    {
        var emulator = new Emulator();

        emulator.A = 0b00000010;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x4a, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0b00000001, 0x00, 0x00, 0x812, 2);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task A_CarrySet()
    {
        var emulator = new Emulator();

        emulator.A = 0b00000010;
        emulator.Carry = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x4a, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0b00000001, 0x00, 0x00, 0x812, 2);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task A_SetCarry()
    {
        var emulator = new Emulator();

        emulator.A = 0b00000011;
        emulator.Carry = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x4a, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0b00000001, 0x00, 0x00, 0x812, 2);
        emulator.AssertFlags(false, false, false, true);
    }

    [TestMethod]
    public async Task A_ShiftZero_SetCarry()
    {
        var emulator = new Emulator();

        emulator.A = 0b00000001;
        emulator.Carry = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x4a, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0b00000000, 0x00, 0x00, 0x812, 2);
        emulator.AssertFlags(true, false, false, true);
    }

    [TestMethod]
    public async Task Abs()
    {
        var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b00000010;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $1234
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x4e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000001, emulator.Memory[0x1234]);

        emulator.AssertState(0x00, 0x00, 0x00, 0x814, 6);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Abs_CarrySet()
    {
        var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b00000010;
        emulator.Carry = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $1234
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x4e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000001, emulator.Memory[0x1234]);

        emulator.AssertState(0x00, 0x00, 0x00, 0x814, 6);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Abs_SetCarry()
    {
        var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b00000011;
        emulator.Carry = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $1234
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x4e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000001, emulator.Memory[0x1234]);

        emulator.AssertState(0x00, 0x00, 0x00, 0x814, 6);
        emulator.AssertFlags(false, false, false, true);
    }

    [TestMethod]
    public async Task Abs_ShiftZero_SetCarry()
    {
        var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b00000001;
        emulator.Carry = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $1234
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x4e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000000, emulator.Memory[0x1234]);

        emulator.AssertState(0x00, 0x00, 0x00, 0x814, 6);
        emulator.AssertFlags(true, false, false, true);
    }

    [TestMethod]
    public async Task AbsX()
    {
        var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b00000010;
        emulator.X = 0x04;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $1230, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x5e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000001, emulator.Memory[0x1234]);

        emulator.AssertState(0x00, 0x04, 0x00, 0x814, 7);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task AbsX_CarrySet()
    {
        var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b00000010;
        emulator.Carry = true;
        emulator.X = 0x04;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $1230, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x5e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000001, emulator.Memory[0x1234]);

        emulator.AssertState(0x00, 0x04, 0x00, 0x814, 7);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task AbsX_SetCarry()
    {
        var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b00000011;
        emulator.Carry = false;
        emulator.X = 0x04;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $1230, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x5e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000001, emulator.Memory[0x1234]);

        emulator.AssertState(0x00, 0x04, 0x00, 0x814, 7);
        emulator.AssertFlags(false, false, false, true);
    }

    [TestMethod]
    public async Task AbsX_ShiftZero_SetCarry()
    {
        var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b00000001;
        emulator.Carry = false;
        emulator.X = 0x04;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $1230, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x5e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000000, emulator.Memory[0x1234]);

        emulator.AssertState(0x00, 0x04, 0x00, 0x814, 7);
        emulator.AssertFlags(true, false, false, true);
    }

    [TestMethod]
    public async Task Zp()
    {
        var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000010;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $12
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x46, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000001, emulator.Memory[0x12]);

        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 5);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Zp_CarrySet()
    {
        var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000010;
        emulator.Carry = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $12
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x46, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000001, emulator.Memory[0x12]);

        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 5);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Zp_SetCarry()
    {
        var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000011;
        emulator.Carry = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $12
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x46, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000001, emulator.Memory[0x12]);

        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 5);
        emulator.AssertFlags(false, false, false, true);
    }

    [TestMethod]
    public async Task Zp_ShiftZero_SetCarry()
    {
        var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000001;
        emulator.Carry = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $12
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x46, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000000, emulator.Memory[0x12]);

        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 5);
        emulator.AssertFlags(true, false, false, true);
    }

    [TestMethod]
    public async Task ZpX()
    {
        var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000010;
        emulator.X = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $10, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x56, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000001, emulator.Memory[0x12]);

        emulator.AssertState(0x00, 0x02, 0x00, 0x813, 6);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task ZpX_Wrap()
    {
        var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000010;
        emulator.X = 0x72;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $a0, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x56, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000001, emulator.Memory[0x12]);

        emulator.AssertState(0x00, 0x72, 0x00, 0x813, 6);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task ZpX_CarrySet()
    {
        var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000010;
        emulator.Carry = true;
        emulator.X = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $10, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x56, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000001, emulator.Memory[0x12]);

        emulator.AssertState(0x00, 0x02, 0x00, 0x813, 6);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task ZpX_SetCarry()
    {
        var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000011;
        emulator.Carry = false;
        emulator.X = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $10, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x56, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000001, emulator.Memory[0x12]);

        emulator.AssertState(0x00, 0x02, 0x00, 0x813, 6);
        emulator.AssertFlags(false, false, false, true);
    }

    [TestMethod]
    public async Task ZpX_ShiftZero_SetCarry()
    {
        var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000001;
        emulator.Carry = false;
        emulator.X = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lsr $10, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x56, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000000, emulator.Memory[0x12]);

        emulator.AssertState(0x00, 0x02, 0x00, 0x813, 6);
        emulator.AssertFlags(true, false, false, true);
    }
}