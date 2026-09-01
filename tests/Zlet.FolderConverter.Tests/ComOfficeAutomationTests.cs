using System.Runtime.InteropServices;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.OfficeWorker;

namespace Zlet.FolderConverter.Tests;

public sealed class ComOfficeAutomationTests
{
    [Fact]
    public void Word_does_not_require_hWnd_com_property()
    {
        var session = new ComOfficeAutomationSession(
            OfficeApplicationKind.Word,
            new object());

        Assert.Equal(0, session.WindowHandle);
    }

    [Fact]
    public void Powerpoint_does_not_require_hWnd_com_property()
    {
        var session = new ComOfficeAutomationSession(
            OfficeApplicationKind.PowerPoint,
            new object());

        Assert.Equal(0, session.WindowHandle);
    }

    [Fact]
    public void Powerpoint_configuration_does_not_try_to_hide_application()
    {
        var application = new FakePowerPointApplication();
        var session = new ComOfficeAutomationSession(
            OfficeApplicationKind.PowerPoint,
            application);

        session.Configure();

        Assert.False(application.VisibleWasSet);
        Assert.Equal(1, application.DisplayAlerts);
        Assert.Equal(3, application.AutomationSecurity);
    }

    [Fact]
    public void Powerpoint_save_uses_embed_true_type_fonts_parameter()
    {
        var application = new FakePowerPointApplication();
        var session = new ComOfficeAutomationSession(
            OfficeApplicationKind.PowerPoint,
            application);

        session.OpenAndSave(new OfficeWorkerRequest(
            OfficeApplicationKind.PowerPoint,
            "source.ppt",
            "result.pptx"));

        Assert.Equal("source.ppt", application.Presentations.SourcePath);
        Assert.Equal("result.pptx", application.Presentations.Presentation.OutputPath);
        Assert.Equal(24, application.Presentations.Presentation.FileFormat);
        Assert.Equal(0, application.Presentations.Presentation.EmbedTrueTypeFonts);
        Assert.True(application.Presentations.Presentation.CloseCalled);
    }

    [Fact]
    public void Powerpoint_already_running_does_not_create_com()
    {
        var events = new List<string>();
        var factory = new FakeSessionFactory(events);
        var automation = new ComOfficeAutomation(
            factory,
            new FakeProcessIdentity(events, powerPointAlreadyRunning: true));
        var reported = new List<OfficeWorkerMessage>();

        var result = automation.ConvertPowerPoint(
            Request(OfficeApplicationKind.PowerPoint),
            reported.Add);

        Assert.False(result.Success);
        Assert.Equal("powerpoint_already_running", result.ErrorCode);
        Assert.Equal(0, factory.CreateCount);
        Assert.Empty(reported);
        Assert.DoesNotContain("configure", events);
        Assert.DoesNotContain("cleanup", events);
    }

    [Theory]
    [InlineData(OfficeApplicationKind.Word)]
    [InlineData(OfficeApplicationKind.Excel)]
    public void Running_powerpoint_does_not_block_word_or_excel(
        OfficeApplicationKind application)
    {
        var events = new List<string>();
        var factory = new FakeSessionFactory(events);
        var automation = new ComOfficeAutomation(
            factory,
            new FakeProcessIdentity(events, powerPointAlreadyRunning: true));

        var result = Convert(automation, application, _ => { });
        automation.Dispose();

        Assert.True(result.Success);
        Assert.Equal(1, factory.CreateCount);
        Assert.Contains("configure", events);
        Assert.Contains("open_and_save", events);
        Assert.Contains("cleanup", events);
    }

    [Theory]
    [InlineData(OfficeApplicationKind.Word)]
    [InlineData(OfficeApplicationKind.Excel)]
    [InlineData(OfficeApplicationKind.PowerPoint)]
    public void Started_is_reported_before_office_configuration(
        OfficeApplicationKind application)
    {
        var events = new List<string>();
        var factory = new FakeSessionFactory(events);
        var automation = new ComOfficeAutomation(
            factory,
            new FakeProcessIdentity(events));

        var result = Convert(
            automation,
            application,
            _ => events.Add("started_reported"));
        automation.Dispose();

        Assert.True(result.Success);
        Assert.True(events.IndexOf("window_handle") < events.IndexOf("started_reported"));
        Assert.True(events.IndexOf("started_reported") < events.IndexOf("configure"));
        Assert.True(events.IndexOf("configure") < events.IndexOf("open_and_save"));
        Assert.True(events.IndexOf("open_and_save") < events.IndexOf("cleanup"));
    }

    [Theory]
    [InlineData(OfficeApplicationKind.Word)]
    [InlineData(OfficeApplicationKind.Excel)]
    [InlineData(OfficeApplicationKind.PowerPoint)]
    public void Multiple_files_reuse_one_com_session_until_dispose(
        OfficeApplicationKind application)
    {
        var events = new List<string>();
        var factory = new FakeSessionFactory(events);
        var automation = new ComOfficeAutomation(
            factory,
            new FakeProcessIdentity(events));
        var reported = new List<OfficeWorkerMessage>();

        var first = Convert(automation, application, reported.Add);
        var second = Convert(automation, application, reported.Add);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(2, events.Count(item => item == "open_and_save"));
        Assert.Single(reported);
        Assert.DoesNotContain("cleanup", events);

        automation.Dispose();

        Assert.Equal(1, events.Count(item => item == "cleanup"));
    }

    [Fact]
    public void Document_com_failure_invalidates_session_and_next_file_can_recover()
    {
        var events = new List<string>();
        var factory = new FakeSessionFactory(events)
        {
            OpenFailuresRemaining = 1
        };
        var automation = new ComOfficeAutomation(
            factory,
            new FakeProcessIdentity(events));

        var first = automation.ConvertWord(Request(OfficeApplicationKind.Word), _ => { });
        var second = automation.ConvertWord(Request(OfficeApplicationKind.Word), _ => { });
        automation.Dispose();

        Assert.False(first.Success);
        Assert.True(first.SessionInvalid);
        Assert.Equal("office_com_failure", first.ErrorCode);
        Assert.True(second.Success);
        Assert.Equal(2, factory.CreateCount);
        Assert.Equal(2, events.Count(item => item == "cleanup"));
    }

    [Fact]
    public void User_powerpoint_content_appearing_between_files_is_never_quit()
    {
        var events = new List<string>();
        var factory = new FakeSessionFactory(events)
        {
            HasOpenDocuments = true
        };
        var automation = new ComOfficeAutomation(
            factory,
            new FakeProcessIdentity(events));

        var first = automation.ConvertPowerPoint(
            Request(OfficeApplicationKind.PowerPoint),
            _ => { });
        var second = automation.ConvertPowerPoint(
            Request(OfficeApplicationKind.PowerPoint),
            _ => { });
        automation.Dispose();

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal("powerpoint_session_ownership_lost", second.ErrorCode);
        Assert.True(second.SessionInvalid);
        Assert.True(second.AbandonOfficeProcessOwnership);
        Assert.Equal(1, events.Count(item => item == "open_and_save"));
        Assert.Contains("abandon", events);
        Assert.DoesNotContain("cleanup", events);
    }

    [Fact]
    public void Batch_cleanup_never_quits_powerpoint_after_user_content_appears()
    {
        var events = new List<string>();
        var automation = new ComOfficeAutomation(
            new FakeSessionFactory(events)
            {
                HasOpenDocuments = true
            },
            new FakeProcessIdentity(events));

        var result = automation.ConvertPowerPoint(
            Request(OfficeApplicationKind.PowerPoint),
            _ => { });
        automation.Dispose();

        Assert.True(result.Success);
        Assert.Contains("abandon", events);
        Assert.DoesNotContain("cleanup", events);
    }

    [Theory]
    [InlineData(OfficeApplicationKind.Word)]
    [InlineData(OfficeApplicationKind.Excel)]
    [InlineData(OfficeApplicationKind.PowerPoint)]
    public void Cleanup_runs_after_com_failure(
        OfficeApplicationKind application)
    {
        var events = new List<string>();
        var factory = new FakeSessionFactory(events)
        {
            ThrowDuringConfigure = true
        };
        var automation = new ComOfficeAutomation(
            factory,
            new FakeProcessIdentity(events));

        var result = Convert(automation, application, _ => { });

        Assert.False(result.Success);
        Assert.Equal("office_com_failure", result.ErrorCode);
        Assert.Equal(unchecked((int)0x80004005), result.HResult);
        Assert.Contains("cleanup", events);
        Assert.DoesNotContain("open_and_save", events);
    }

    [Fact]
    public void Cleanup_exception_does_not_hide_primary_com_failure()
    {
        var events = new List<string>();
        var factory = new FakeSessionFactory(events)
        {
            ThrowDuringConfigure = true,
            ThrowDuringCleanup = true
        };
        var automation = new ComOfficeAutomation(
            factory,
            new FakeProcessIdentity(events));

        var result = automation.ConvertWord(
            Request(OfficeApplicationKind.Word),
            _ => { });

        Assert.False(result.Success);
        Assert.Equal("office_com_failure", result.ErrorCode);
        Assert.Equal(unchecked((int)0x80004005), result.HResult);
        Assert.Contains("cleanup", events);
    }

    private static OfficeWorkerRequest Request(OfficeApplicationKind application) =>
        new(application, "source", "output");

    private static OfficeWorkerMessage Convert(
        ComOfficeAutomation automation,
        OfficeApplicationKind application,
        Action<OfficeWorkerMessage> report) =>
        application switch
        {
            OfficeApplicationKind.Word => automation.ConvertWord(Request(application), report),
            OfficeApplicationKind.Excel => automation.ConvertExcel(Request(application), report),
            OfficeApplicationKind.PowerPoint =>
                automation.ConvertPowerPoint(Request(application), report),
            _ => throw new ArgumentOutOfRangeException(nameof(application))
        };

    public sealed class FakePowerPointApplication
    {
        public bool VisibleWasSet { get; private set; }
        public int DisplayAlerts { get; set; }
        public int AutomationSecurity { get; set; }
        public FakePowerPointPresentations Presentations { get; } = new();

        public int Visible
        {
            set
            {
                VisibleWasSet = true;
                throw new COMException(
                    "Application.Visible: hiding is not allowed.",
                    unchecked((int)0x80048240));
            }
        }
    }

    public sealed class FakePowerPointPresentations
    {
        public string SourcePath { get; private set; } = string.Empty;
        public FakePowerPointPresentation Presentation { get; } = new();

        public FakePowerPointPresentation Open(
            string FileName,
            int ReadOnly,
            int Untitled,
            int WithWindow)
        {
            SourcePath = FileName;
            return Presentation;
        }
    }

    public sealed class FakePowerPointPresentation
    {
        public string OutputPath { get; private set; } = string.Empty;
        public int FileFormat { get; private set; }
        public int EmbedTrueTypeFonts { get; private set; }
        public bool CloseCalled { get; private set; }

        public void SaveAs(
            string FileName,
            int FileFormat,
            int EmbedTrueTypeFonts)
        {
            OutputPath = FileName;
            this.FileFormat = FileFormat;
            this.EmbedTrueTypeFonts = EmbedTrueTypeFonts;
        }

        public void Close() => CloseCalled = true;
    }

    private sealed class FakeSessionFactory(List<string> events)
        : IOfficeAutomationSessionFactory
    {
        public int CreateCount { get; private set; }
        public bool ThrowDuringConfigure { get; init; }
        public bool ThrowDuringCleanup { get; init; }
        public int OpenFailuresRemaining { get; set; }
        public bool HasOpenDocuments { get; init; }

        public IOfficeAutomationSession Create(OfficeApplicationKind application)
        {
            CreateCount++;
            events.Add($"create:{application}");
            return new FakeSession(
                events,
                ThrowDuringConfigure,
                ThrowDuringCleanup,
                OpenFailuresRemaining-- > 0,
                HasOpenDocuments);
        }
    }

    private sealed class FakeSession(
        List<string> events,
        bool throwDuringConfigure,
        bool throwDuringCleanup,
        bool throwDuringOpen,
        bool hasOpenDocuments)
        : IOfficeAutomationSession
    {
        public long WindowHandle
        {
            get
            {
                events.Add("window_handle");
                return 123;
            }
        }

        public void Configure()
        {
            events.Add("configure");
            if (throwDuringConfigure)
            {
                throw new COMException(
                    "Synthetic COM failure.",
                    unchecked((int)0x80004005));
            }
        }

        public void OpenAndSave(OfficeWorkerRequest request)
        {
            events.Add("open_and_save");
            if (throwDuringOpen)
            {
                throw new COMException(
                    "Synthetic document failure.",
                    unchecked((int)0x80004005));
            }
        }

        public bool HasOpenDocuments
        {
            get
            {
                events.Add("has_open_documents");
                return hasOpenDocuments;
            }
        }

        public void Cleanup()
        {
            events.Add("cleanup");
            if (throwDuringCleanup)
            {
                throw new COMException("Synthetic cleanup failure.");
            }
        }

        public void Abandon() => events.Add("abandon");
    }

    private sealed class FakeProcessIdentity(
        List<string> events,
        bool powerPointAlreadyRunning = false)
        : IOfficeProcessIdentityProvider
    {
        private int _powerPointCaptureCount;

        public IReadOnlySet<int> Capture(OfficeApplicationKind application)
        {
            events.Add($"capture:{application}");
            if (application != OfficeApplicationKind.PowerPoint)
            {
                return new HashSet<int>();
            }

            _powerPointCaptureCount++;
            if (powerPointAlreadyRunning)
            {
                return new HashSet<int> { 42 };
            }

            return _powerPointCaptureCount == 1
                ? new HashSet<int>()
                : new HashSet<int> { 4242 };
        }

        public OfficeWorkerMessage CreateStartedMessage(
            OfficeApplicationKind application,
            long windowHandle,
            IReadOnlySet<int> baseline)
        {
            events.Add("started_created");
            return new OfficeWorkerMessage(
                OfficeWorkerMessageType.Started,
                OfficeProcessId: 4242,
                OfficeProcessStartTimeUtcTicks: 638900000000000000,
                OfficeProcessOwned: true);
        }
    }
}
