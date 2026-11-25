"use strict";

let execution;
let timeout;
let result = "";
let output = "";
const maxOutputLength = 100000;

function finish(line) {
    clearTimeout(timeout);
    execution?.terminate();
    execution = undefined;
    result = output + "\n" + line;
}

function runScript(source) {
    execution?.terminate();
    result = "";
    output = "";
    execution = new Worker("worker.js");
    timeout = setTimeout(() => finish("[Python startup timed out]"), 30000);
    execution.onmessage = ({ data }) => {
        if (data.ready) {
            clearTimeout(timeout);
            timeout = setTimeout(() => finish("[timed out - execution stopped]"), 15000);
            execution.postMessage(source);
        } else if (data.line !== undefined && output.length < maxOutputLength) {
            const line = data.line + "\n";
            output += line.slice(0, maxOutputLength - output.length);
            if (output.length === maxOutputLength) output += "\n[output truncated]\n";
        } else if (data.done) {
            finish(data.done);
        }
    };
    execution.onerror = (event) => finish("[Python error: " + event.message + "]");
}

function getResult() {
    return result;
}
