module LCU

open System.Management
open System.IO

(*
 * TODO
 * macOS, linux (with league under wine) support
 *)

type ConnectionInfo =
    { PID: int
      Port: int
      Password: string }

type ConnectionState = { Info: ClientInfo }

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

let clientCli () =
    (* this is very procedural... *)
    use query = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE Name='LeagueClientUx.exe'")
    use res = query.Get()
    let resEnum = res.GetEnumerator()
    if resEnum.MoveNext() then
        resEnum.Current.["CommandLine"].ToString()
        |> argsList
        |> Ok
    else
        Error "No such process (is your client open?)."

(* DEPRECATED get port from lockfile instead *)
let cliPort (cli: string list) =
    cli
    |> List.filter (fun arg -> arg.StartsWith("--app-port="))
    |> List.head
    |> (fun str -> str.Split('=').[1])
    |> int

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
      Password = xs.[3] }

let loadLockfile =
    List.head >> lockfileFromExec >> readFile >> parseLockfile

(* FIXME no connection is actually estabilished here *)
let maybeConnect () =
    match clientCli() with
    | Ok cli ->
        try
            let info = loadLockfile cli
            Ok { Info = info }
        with ex ->
            Error ex.Message
    | Error e -> Error e (* FIXME can we not repack the error? *)
