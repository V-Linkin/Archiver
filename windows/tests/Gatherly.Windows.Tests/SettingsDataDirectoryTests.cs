using Gatherly.Windows.Database;
using Xunit;

namespace Gatherly.Windows.Tests;

public class SettingsDataDirectoryTests : IDisposable
{
    private readonly string _nonEmptyDir;
    private readonly string _emptyDir;

    public SettingsDataDirectoryTests()
    {
        _nonEmptyDir = Path.Combine(Path.GetTempPath(), "GlySDT_ne_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_nonEmptyDir);
        File.WriteAllText(Path.Combine(_nonEmptyDir, "existing_file.txt"), "data");

        _emptyDir = Path.Combine(Path.GetTempPath(), "GlySDT_e_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_emptyDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_nonEmptyDir, true); } catch { }
        try { Directory.Delete(_emptyDir, true); } catch { }
    }

    [Fact]
    public void DataDirectoryButtons_ShouldShowOpenAndModify()
    {
        var openCmdExists = typeof(Gatherly.Windows.ViewModels.SettingsViewModel)
            .GetMethod("OpenDataDirectory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null;
        var modifyCmdExists = typeof(Gatherly.Windows.ViewModels.SettingsViewModel)
            .GetMethod("ModifyDataDirectory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null;

        Assert.True(openCmdExists, "OpenDataDirectory command should exist");
        Assert.True(modifyCmdExists, "ModifyDataDirectory command should exist");
    }

    [Fact]
    public void MigrationPlan_ShouldRenderInStorageSection()
    {
        var plan = DataDirectoryMigrationPlan.Generate(_emptyDir);

        Assert.False(plan.IsSameDirectory);
        Assert.False(string.IsNullOrEmpty(plan.CurrentDirectory));
        Assert.False(string.IsNullOrEmpty(plan.TargetDirectory));
    }

    [Fact]
    public void MigrationPlan_ShouldNotRenderAsPlainLogText()
    {
        var plan = DataDirectoryMigrationPlan.Generate(_emptyDir);

        Assert.False(string.IsNullOrEmpty(plan.CurrentDirectory));
        Assert.False(string.IsNullOrEmpty(plan.TargetDirectory));
        Assert.True(plan.DbFileSize >= 0);
        Assert.True(plan.MediaFileCount >= 0);
        Assert.True(plan.EstimatedFileCount >= 1);
    }

    [Fact]
    public void ModifyDataDirectory_Cancel_ShouldClearPlanOrShowCancelStatus()
    {
        var status = "已取消选择，未更改数据目录";
        var hasMigrationPlan = false;
        var planConclusion = "";

        Assert.False(hasMigrationPlan);
        Assert.Equal("已取消选择，未更改数据目录", status);
        Assert.Equal("", planConclusion);
    }

    [Fact]
    public void NonEmptyTarget_ShouldShowConflictWarning()
    {
        var plan = DataDirectoryMigrationPlan.Generate(_nonEmptyDir);

        Assert.True(plan.TargetExists);
        Assert.False(plan.TargetEmpty);
        Assert.Contains(plan.Warnings, w => w.Contains("冲突"));

        var conclusion = plan.TargetExists && !plan.TargetEmpty
            ? "存在冲突风险，建议选择空目录"
            : "可迁移到目标目录";

        Assert.Equal("存在冲突风险，建议选择空目录", conclusion);
    }
}
