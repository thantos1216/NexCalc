using System.Text.Json;
using Android.Views;
using Android.Webkit;
using Android.Widget;

namespace NexCalc.Mobile;

public sealed partial class MainActivity
{
    private WebView? pythonEngine;

    private View PythonPanel()
    {
        var panel = Column();
        var source = Field("Python script", "print('Hello from NexCalc')", multiline: true);
        source.SetMinHeight(0);
        source.SetMinimumHeight(0);
        source.Gravity = GravityFlags.Top | GravityFlags.Start;
        var output = Label("");
        output.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
        output.SetTextIsSelectable(true);
        output.Typeface = global::Android.Graphics.Typeface.Monospace;
        var running = false;
        Button run = null!;
        run = Key("Run", () =>
        {
            if (running) return;
            running = true;
            run.Enabled = false;
            output.Text = "Starting Python…";

            // A fresh WebView and Worker give each script the same clean start as a desktop process.
            pythonEngine = new WebView(this);
            pythonEngine.Settings.JavaScriptEnabled = true;
            pythonEngine.Settings.AllowFileAccess = false;
            pythonEngine.Settings.AllowContentAccess = false;
            pythonEngine.Settings.BlockNetworkLoads = true;

            void Complete(string result)
            {
                if (!running) return;
                running = false;
                run.Enabled = true;
                output.Text = result;
                if (pythonEngine is { } engine)
                {
                    panel.RemoveView(engine);
                    engine.Destroy();
                }
                pythonEngine = null;
            }

            var deadline = Environment.TickCount64 + 50000;
            void Poll()
            {
                var engine = pythonEngine;
                if (engine is null) return;
                if (Environment.TickCount64 >= deadline)
                {
                    Complete("[Python engine timed out]");
                    return;
                }
                engine.EvaluateJavascript("getResult()", new JsResult(value =>
                {
                    string? result = value is null or "null" ? null : JsonSerializer.Deserialize<string>(value);
                    if (!string.IsNullOrEmpty(result)) Complete(result);
                    else run.PostDelayed(Poll, 200);
                }));
            }

            var script = JsonSerializer.Serialize(source.Text ?? "");
            pythonEngine.SetWebViewClient(new PythonAssets(this, view =>
            {
                view.EvaluateJavascript("runScript(" + script + ")", null);
                run.PostDelayed(Poll, 200);
            }, Complete));
            panel.AddView(pythonEngine, new LinearLayout.LayoutParams(1, 1));
            pythonEngine.LoadUrl(PythonAssets.Entry);
        });
        var outputScroll = new ScrollView(this) { FillViewport = true };
        outputScroll.AddView(output);
        panel.AddView(source, new LinearLayout.LayoutParams(-1, 0, 1));
        panel.AddView(run);
        panel.AddView(Label("Output"));
        ApplyThemeTo(() => outputScroll.SetBackgroundColor(lightTheme
            ? global::Android.Graphics.Color.Rgb(232, 238, 245)
            : global::Android.Graphics.Color.Rgb(34, 34, 52)));
        panel.AddView(outputScroll, new LinearLayout.LayoutParams(-1, 0, 2));
        return panel;
    }

    protected override void OnDestroy()
    {
        if (pythonEngine is { } engine)
        {
            (engine.Parent as ViewGroup)?.RemoveView(engine);
            engine.Destroy();
            pythonEngine = null;
        }
        base.OnDestroy();
    }

    private sealed class JsResult(Action<string?> received) : Java.Lang.Object, IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value) => received(value?.ToString());
    }

    private sealed class PythonAssets(MainActivity activity, Action<WebView> loaded, Action<string> error) : WebViewClient
    {
        public const string Entry = "https://nexcalc.invalid/python/index.html";

        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request) => true;

        public override WebResourceResponse ShouldInterceptRequest(WebView? view, IWebResourceRequest? request)
        {
            var uri = request?.Url;
            if (uri?.Scheme != "https" || uri.Host != "nexcalc.invalid" ||
                uri.Path is not { } path || !path.StartsWith("/python/", StringComparison.Ordinal) || path.Contains(".."))
                return new WebResourceResponse("text/plain", "utf-8", 403, "Forbidden", new Dictionary<string, string>(), Stream.Null);

            var mime = Path.GetExtension(path) switch
            {
                ".html" => "text/html",
                ".js" => "text/javascript",
                ".wasm" => "application/wasm",
                ".json" => "application/json",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
            try
            {
                return new WebResourceResponse(mime, mime is "application/wasm" or "application/zip" ? null : "utf-8",
                    activity.Assets!.Open(path[1..]));
            }
            catch (Java.IO.IOException)
            {
                return new WebResourceResponse("text/plain", "utf-8", 404, "Not Found", new Dictionary<string, string>(), Stream.Null);
            }
        }

        public override void OnPageFinished(WebView? view, string? url)
        {
            if (view is not null && url == Entry) loaded(view);
        }

        public override void OnReceivedError(WebView? view, IWebResourceRequest? request, WebResourceError? failure)
        {
            if (request?.IsForMainFrame == true)
                error("[Unable to start Python: " + failure?.Description + "]");
        }
    }
}
