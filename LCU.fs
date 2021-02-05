module LCU

(* for F# repl do
 * #r "nuget: Microsoft.Windows.Compatibility, 5.0.1"
 * #r "nuget: Newtonsoft.Json, 12.0.3"
 *)

open System
open System.Diagnostics
open System.IO
open System.Management
open System.Net.Http
open System.Net.Http.Headers
open System.Runtime.InteropServices
open System.Text

open Newtonsoft.Json

(*
 * TODO
 * macOS, linux (with league under wine) support
 * events
 *)

type ConnectionInfo =
    { PID: int
      Port: int
      Proto: string
      Password: string }

type ConnectionState = { Info: ConnectionInfo
                         Client: HttpClient }

(* FIXME? does not account for escaped quotes *)
let argsList (s: string) =
    let xs = s.Split '"'
    [0..xs.Length-1] (* we need the indexes *)
    |> List.map (fun idx ->
                 let str = xs.[idx]
                 match idx%2 with
                 | 0 -> str.Split ' ' |> Array.toList
                 | _ -> [str])
    |> List.concat
    |> List.filter ((<>) "")

let subshell (exec: string) =
    let proc = new Process()
    proc.StartInfo.FileName <- exec
    proc.StartInfo.RedirectStandardInput <- true
    proc.StartInfo.RedirectStandardOutput <- true
    proc.StartInfo.UseShellExecute <- false
    proc.Start() |> ignore
    proc

let shell (line: string) =                                
    let proc = subshell "/bin/sh"
    
    proc.StandardInput.WriteLine(line)
    proc.StandardInput.Flush()
    proc.StandardInput.Close()
    proc.WaitForExit()
    proc.StandardOutput.ReadToEnd()

let split (d: char) (str: string) =
    str.Split d

let clientCli () =
    match Environment.OSVersion.Platform with
    | PlatformID.Win32NT ->
        use query = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE Name='LeagueClientUx.exe'")
        use res = query.Get()
        let resEnum = res.GetEnumerator()
        if resEnum.MoveNext() then
            resEnum.Current.["CommandLine"].ToString()
            |> argsList
            |> Ok
        else
            Error "No such process (is your client open?)."
    | PlatformID.Unix -> (* TODO *)
        if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
            shell "ps x -o args | grep 'LeagueClientUx'" (* FIXME has spaces so it doesn't work *)
            |> split '\n'
            |> Array.head
            |> argsList
            |> Ok
        else (* "standard" UNIX with LoL under Wine *)
            Error "Unsupported platform."
    | _ ->
        Error "Unsupported platform."

let lockfileFromExec (exec: string) =
    Path.Combine(Path.GetDirectoryName(exec), "lockfile")

let readFile path =
    use fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
    use sr = new StreamReader(fs)
    sr.ReadToEnd()

let parseLockfile (text: string) =
    let xs = text.Split ':'
    { PID      = xs.[1] |> int
      Port     = xs.[2] |> int
      Password = xs.[3]
      Proto    = xs.[4] }

let loadLockfile =
    List.head >> lockfileFromExec >> readFile >> parseLockfile

(* FIXME no connection is actually estabilished here *)
let maybeConnect () =
    match clientCli() with
    | Ok cli ->
        try
            let info = loadLockfile cli
            let handler = new HttpClientHandler()
            (* >tfw descriptive names *)
            (* we could instead try to validate the certificate based on the root one offered by riot *)
            handler.ServerCertificateCustomValidationCallback <- HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            let passwd = Encoding.ASCII.GetBytes($"riot:{info.Password}")
            let client = new HttpClient(handler)
            client.DefaultRequestHeaders.Authorization <- new AuthenticationHeaderValue("Basic", Convert.ToBase64String(passwd))
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"))
            Ok { Info   = info
                 Client = client }
        with ex ->
            Error ex.Message
    | Error e -> Error e (* FIXME can we not repack the error? *)

let cleanup con =
    con.Client.Dispose()

let endpointUrl i e =
    (* FIXME there's no validation on the URL or anything *)
    $"{i.Proto}://127.0.0.1:{i.Port}{e}"

let getAsync state endpoint =
    async {
        let url = endpointUrl state.Info endpoint
        use! response = state.Client.GetAsync(url) |> Async.AwaitTask
        response.EnsureSuccessStatusCode() |> ignore
        let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        return body
    }

let get s e =
    getAsync s e |> Async.RunSynchronously

let postAsync state endpoint data =
    async {
        let url = endpointUrl state.Info endpoint
        let json = JsonConvert.SerializeObject(data)
        use content = new StringContent(json, Encoding.UTF8, "application/json")
        let! response = state.Client.PostAsync(url, content) |> Async.AwaitTask
        response.EnsureSuccessStatusCode() |> ignore
        let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        return body
    }

let post s e d =
    postAsync s e d |> Async.RunSynchronously
