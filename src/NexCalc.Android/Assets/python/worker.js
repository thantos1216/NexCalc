"use strict";

importScripts("runtime/pyodide.js");

const python = loadPyodide({ indexURL: new URL("runtime/", self.location.href).href });
python.then(() => postMessage({ ready: true }),
    error => postMessage({ done: "[Python startup error: " + error.message + "]" }));

self.onmessage = async ({ data: source }) => {
    const runtime = await python;
    runtime.setStdout({ batched: line => postMessage({ line }) });
    runtime.setStderr({ batched: line => postMessage({ line: "[stderr] " + line }) });
    runtime.globals.set("_nexcalc_source", source);
    try {
        const code = runtime.runPython(`
import sys as _sys
import traceback as _traceback
import types as _types

_module = _types.ModuleType("__main__")
_module.__file__ = "<NexCalc>"
_sys.modules["__main__"] = _module
_status = 0
try:
    exec(compile(_nexcalc_source, "<NexCalc>", "exec"), _module.__dict__)
except SystemExit as _exit:
    if isinstance(_exit.code, int):
        _status = _exit.code
    elif _exit.code is not None:
        print(_exit.code, file=_sys.stderr)
        _status = 1
except BaseException:
    _traceback.print_exc()
    _status = 1
_status
`);
        postMessage({ done: "[exit code " + code + "]" });
    } catch (error) {
        postMessage({ done: "[Python error: " + error.message + "]" });
    }
};
