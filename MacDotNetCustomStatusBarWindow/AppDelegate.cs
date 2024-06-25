namespace MacDotNetCustomStatusBarWindow;

[Register("AppDelegate")]
public class AppDelegate : NSApplicationDelegate
{

	NSStatusItem statusItem;
	StatusBarMenuWindowController windowController;
	public override void DidFinishLaunching(NSNotification notification)
	{
		// Insert code here to initialize your application
		statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Square);
		statusItem.Button.Title = "😊";
		statusItem.Button.Activated += (sender, e) =>
		{
			if (windowController == null || windowController.Window.IsVisible == false)
			{
				ShowUI((NSObject)sender);
			}
			else
			{
				HideUI();
			}
		};
	}

	public override void WillTerminate(NSNotification notification)
	{
		// Insert code here to tear down your application
	}

	private void HideUI()
	{
		windowController.Close();
	}

	private void ShowUI(NSObject sender)
	{
		if (windowController == null)
		{
			windowController = new StatusBarMenuWindowController(statusItem, new DummyContentViewController());
		}

		windowController.ShowWindow(sender);
	}
}

public class DummyContentViewController : NSViewController
{
	public DummyContentViewController() : base()
	{
		PreferredContentSize = new CoreGraphics.CGSize(290, 300);
	}

	public override void LoadView()
	{
		View = new NSView
		{
			WantsLayer = true,
			Layer = { BackgroundColor = NSColor.WindowBackground.CGColor }
		};
	}
}

public class StatusBarMenuWindowController : NSWindowController, INSWindowDelegate
{
	private NSStatusItem statusItem;
	private NSViewController contentViewController;
	private EventMonitor eventMonitor;

	public StatusBarMenuWindowController(NSStatusItem statusItem, NSViewController contentViewController)
	{
		this.statusItem = statusItem;
		this.contentViewController = contentViewController;

		this.Window = new NSWindow(
			new CoreGraphics.CGRect(0, 0, 344, 320),
			NSWindowStyle.FullSizeContentView | NSWindowStyle.Titled,
			NSBackingStore.Buffered,
			false,
			screen: statusItem!.Button!.Window!.Screen
		);

		this.Window.MovableByWindowBackground = false;
		this.Window.TitleVisibility = NSWindowTitleVisibility.Hidden;
		this.Window.TitlebarAppearsTransparent = true;
		this.Window.Level = NSWindowLevel.Status;
		this.Window.ContentViewController = contentViewController;

		if (NSProcessInfo.ProcessInfo.IsOperatingSystemAtLeastVersion(new NSOperatingSystemVersion(11, 0, 0)))
		{
			this.Window.IsOpaque = false;
			this.Window.BackgroundColor = NSColor.Clear;
		}

		this.Window.Delegate = this;
		this.RepositionWindow();
	}

	public override void ShowWindow(NSObject sender)
	{
		PostBeginMenuTrackingNotification();
		NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
		RepositionWindow();
		Window.AlphaValue = 1;
		base.ShowWindow(sender);
		StartMonitoringClicks();
	}

	public override void Close()
	{
		PostEndMenuTrackingNotification();
		NSAnimationContext.BeginGrouping();
		NSAnimationContext.CurrentContext.CompletionHandler = () =>
		{
			base.Close();
			eventMonitor?.Stop();
			eventMonitor = null;
		};
		//Window.Animator.AlphaValue = 0;
		NSAnimationContext.EndGrouping();
	}

	private void StartMonitoringClicks()
	{
		eventMonitor = new EventMonitor(NSEventMask.LeftMouseDown | NSEventMask.RightMouseDown, (NSEvent theEvent) =>
		{
			Close();
		});
		eventMonitor.Start();
	}

	private void RepositionWindow()
	{
		var referenceWindow = statusItem?.Button?.Window;
		if (referenceWindow == null || this.Window == null)
		{
			Console.WriteLine("Couldn't find reference window for repositioning status bar menu window, centering instead");
			this.Window?.Center();
			return;
		}

		var width = contentViewController?.PreferredContentSize.Width ?? this.Window.Frame.Width;
		var height = contentViewController?.PreferredContentSize.Height ?? this.Window.Frame.Height;
		var x = referenceWindow.Frame.X + (referenceWindow.Frame.Width / 2) - (this.Window.Frame.Width / 2);

		if (referenceWindow.Screen != null)
		{
			var screen = referenceWindow.Screen;
			// If the window extrapolates the limits of the screen, reposition it.
			if ((x + width) > (screen.VisibleFrame.X + screen.VisibleFrame.Width))
			{
				x = (screen.VisibleFrame.X + screen.VisibleFrame.Width) - width - Metrics.Margin;
			}
		}

		var rect = new CoreGraphics.CGRect(
			x: x,
			y: referenceWindow.Frame.Y - height - Metrics.Margin,
			width: width,
			height: height
		);

		this.Window.SetFrame(rect, display: true, animate: false);
	}

	private struct Metrics
	{
		public static readonly nfloat Margin = 5;
	}

	// Implement other methods and properties as needed, including window delegate methods and content size observation

	private void PostBeginMenuTrackingNotification()
	{
		NSDistributedNotificationCenter.DefaultCenter.PostNotificationName("com.apple.HIToolbox.beginMenuTrackingNotification", null);
	}

	private void PostEndMenuTrackingNotification()
	{
		NSDistributedNotificationCenter.DefaultCenter.PostNotificationName("com.apple.HIToolbox.endMenuTrackingNotification", null);
	}

	// Window Delegate Methods
	[Export("windowWillClose:")]
	public void WindowWillClose(NSNotification notification)
	{
		// Your window will close logic here
	}

	[Export("windowDidBecomeKey:")]
	public void WindowDidBecomeKey(NSNotification notification)
	{
		statusItem.Button.Highlight(true);
	}

	[Export("windowDidResignKey:")]
	public void WindowDidResignKey(NSNotification notification)
	{
		statusItem.Button.Highlight(false);
	}
}

public class EventMonitor
{
	private NSObject monitor;
	private NSEventMask mask;
	private Action<NSEvent> handler;

	public EventMonitor(NSEventMask mask, Action<NSEvent> handler)
	{
		this.mask = mask;
		this.handler = handler;
	}

	~EventMonitor()
	{
		Stop();
	}

	public void Start()
	{
		monitor = NSEvent.AddGlobalMonitorForEventsMatchingMask(mask, (theEvent) => handler(theEvent));
	}

	public void Stop()
	{
		if (monitor != null)
		{
			NSEvent.RemoveMonitor(monitor);
			monitor = null;
		}
	}
}