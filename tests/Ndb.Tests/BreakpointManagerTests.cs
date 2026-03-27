using Ndb.Daemon;

namespace Ndb.Tests;

public class BreakpointManagerTests
{
    [Fact]
    public void Add_AssignsIncrementingIds()
    {
        var mgr = new BreakpointManager();
        var bp1 = mgr.Add("Program.cs", 10);
        var bp2 = mgr.Add("Program.cs", 20);

        Assert.Equal(1, bp1.Id);
        Assert.Equal(2, bp2.Id);
    }

    [Fact]
    public void Add_SameFileLine_ReturnsExisting()
    {
        var mgr = new BreakpointManager();
        var bp1 = mgr.Add("Program.cs", 10);
        var bp2 = mgr.Add("Program.cs", 10);

        Assert.Equal(bp1.Id, bp2.Id);
    }

    [Fact]
    public void Remove_ByFileLine_ReturnsTrue()
    {
        var mgr = new BreakpointManager();
        mgr.Add("Program.cs", 10);

        Assert.True(mgr.Remove("Program.cs", 10));
        Assert.Empty(mgr.GetAll());
    }

    [Fact]
    public void Remove_NonExistent_ReturnsFalse()
    {
        var mgr = new BreakpointManager();
        Assert.False(mgr.Remove("Program.cs", 99));
    }

    [Fact]
    public void GetAll_ReturnsAllBreakpoints()
    {
        var mgr = new BreakpointManager();
        mgr.Add("A.cs", 1);
        mgr.Add("B.cs", 2);
        mgr.Add("A.cs", 3);

        var all = mgr.GetAll();
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void Enable_Disable_TogglesState()
    {
        var mgr = new BreakpointManager();
        var bp = mgr.Add("Program.cs", 10);
        Assert.True(bp.Enabled);

        Assert.True(mgr.Disable(bp.Id));
        Assert.False(mgr.FindById(bp.Id)!.Enabled);

        Assert.True(mgr.Enable(bp.Id));
        Assert.True(mgr.FindById(bp.Id)!.Enabled);
    }

    [Fact]
    public void GetEnabledForFile_ExcludesDisabled()
    {
        var mgr = new BreakpointManager();
        var bp1 = mgr.Add("Program.cs", 10);
        var bp2 = mgr.Add("Program.cs", 20);
        mgr.Disable(bp2.Id);

        var enabled = mgr.GetEnabledForFile("Program.cs");
        Assert.Single(enabled);
        Assert.Equal(10, enabled[0].Line);
    }

    [Fact]
    public void GetFilesWithBreakpoints_ReturnsDistinctFiles()
    {
        var mgr = new BreakpointManager();
        mgr.Add("A.cs", 1);
        mgr.Add("B.cs", 2);
        mgr.Add("A.cs", 3);

        var files = mgr.GetFilesWithBreakpoints();
        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void UpdateVerified_SetsVerifiedFlag()
    {
        var mgr = new BreakpointManager();
        var bp = mgr.Add("Program.cs", 10);
        Assert.False(bp.Verified);

        mgr.UpdateVerified("Program.cs", 10, true);
        Assert.True(mgr.FindById(bp.Id)!.Verified);
    }
}
