using System;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using TapSynth.Utils;

namespace TapSynth;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		Logger.Info("App starting");

		AppDomain.CurrentDomain.UnhandledException += (s, ev) => {
			Logger.Exception(ev.ExceptionObject as Exception, "UnhandledException");
		};

		this.DispatcherUnhandledException += (s, ev) => {
			Logger.Exception(ev.Exception, "DispatcherUnhandledException");
		};

		TaskScheduler.UnobservedTaskException += (s, ev) => {
			Logger.Exception(ev.Exception, "UnobservedTaskException");
		};

		Logger.Info($"CurrentDirectory={System.IO.Directory.GetCurrentDirectory()}");
	}

	protected override void OnExit(ExitEventArgs e)
	{
		Logger.Info("App exiting");
		base.OnExit(e);
	}
}

