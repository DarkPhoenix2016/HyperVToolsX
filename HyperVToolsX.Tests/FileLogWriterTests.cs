using HyperVToolsX.Core.Logging;
using Xunit;

namespace HyperVToolsX.Tests;

public class FileLogWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"hvtx-log-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Writes_a_line_per_entry_and_flattens_multiline_messages()
    {
        var writer = new FileLogWriter(_dir);

        writer.Write(new LiveLogEntry(DateTime.Now, LiveLogLevel.Error, "Collection", "HV01", "line one\r\nline two"));

        var text = File.ReadAllText(writer.CurrentFile);
        Assert.Contains("[ERROR] Collection (HV01): line one | line two", text);
        Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Theory]
    [InlineData("connect failed password=hunter2 retry", "hunter2")]
    [InlineData("args: /user:a /Password: s3cret!", "s3cret!")]
    [InlineData("pwd='my secret'", "my secret")]
    public void Redacts_secrets(string message, string secret)
    {
        var writer = new FileLogWriter(_dir);

        writer.Write(new LiveLogEntry(DateTime.Now, LiveLogLevel.Info, "X", null, message));

        Assert.DoesNotContain(secret, File.ReadAllText(writer.CurrentFile));
    }

    [Fact]
    public void Stops_at_the_size_cap_and_notes_it_once()
    {
        var writer = new FileLogWriter(_dir, maxFileBytes: 200);

        for (var i = 0; i < 50; i++)
        {
            writer.Write(new LiveLogEntry(DateTime.Now, LiveLogLevel.Info, "X", null, new string('a', 60)));
        }

        var text = File.ReadAllText(writer.CurrentFile);
        Assert.Equal(1, text.Split("size limit reached").Length - 1);
        Assert.True(new FileInfo(writer.CurrentFile).Length < 1000);
    }

    [Fact]
    public void Old_files_are_pruned_on_start()
    {
        Directory.CreateDirectory(_dir);
        var old = Path.Combine(_dir, "hypervtoolsx-20200101.log");
        File.WriteAllText(old, "x");
        File.SetLastWriteTime(old, DateTime.Now.AddDays(-30));

        _ = new FileLogWriter(_dir, retentionDays: 14);

        Assert.False(File.Exists(old));
    }

    [Fact]
    public void Unwritable_directory_never_throws()
    {
        var writer = new FileLogWriter("Z:\\definitely\\not\\here\\<>");

        writer.Write(new LiveLogEntry(DateTime.Now, LiveLogLevel.Info, "X", null, "hi"));
    }
}
