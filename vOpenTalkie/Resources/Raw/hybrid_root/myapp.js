function ToggleDenoise() {
    const checkbox = document.getElementById('denoise');
    HybridWebView.SendRawMessageToDotNet("denoise;" + checkbox.checked);
}

function HostnameChanged() {
    const hostname = document.getElementById('hostname');
    HybridWebView.SendRawMessageToDotNet("hostname;" + hostname.value);
}

function PortChanged() {
    const port = document.getElementById('port');
    HybridWebView.SendRawMessageToDotNet("port;" + port.value);
}
async function ToggleStream() {
    var result = await HybridWebView.SendInvokeMessageToDotNetAsync("ToggleStream");
    document.getElementById("stream_enable").hidden = result;
    document.getElementById("stream_disable").hidden = !result;
}
async function SetPort() {
    var result = await HybridWebView.SendInvokeMessageToDotNetAsync("FillAttribute", ["port"]);
    document.getElementById("port").value = result;
}
async function SetHostname() {
    var result = await HybridWebView.SendInvokeMessageToDotNetAsync("FillAttribute", ["hostname"]);
    document.getElementById("hostname").value = result;
}

async function SetDenoise() {
    const result = await HybridWebView.SendInvokeMessageToDotNetAsync("GetDenoiseState");
    document.getElementById("denoise").checked = result;
}
async function SetStreamState() {
    const result = await HybridWebView.SendInvokeMessageToDotNetAsync("GetStreamState");
    document.getElementById("stream_enable").hidden = result;
    document.getElementById("stream_disable").hidden = !result;
}

function FillPage() {
    SetPort();
    SetHostname();
    SetDenoise();
    SetStreamState();
}

window.onload = FillPage();