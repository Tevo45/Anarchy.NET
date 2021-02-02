[<EntryPoint>]
let main argv =
    match LCU.maybeConnect() with
    | Ok client ->
        printf "Connected to LCU at port %d, password is %s\n" client.Info.Port client.Info.Password
        0
    | Error e ->
        printf "Unable to connect to LCU API: %s\n" e
        1
