#r "paket: groupref netcorebuild //"
#load ".fake/build.fsx/intellisense.fsx"

#nowarn "52"

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.JavaScript


let deployDir = "./deploy" |> Path.getFullName
let staticDir = "./static/assets" |> Path.getFullName
let dockerUser = "evelina"
let dockerOrg = "turinginst"
let dockerImageName = "twitcher"

let localConfig = staticDir + "/api-config.yaml"

Target.create "Clean" (fun _ ->
    !! "src/bin"
    ++ "src/obj"
    ++ "output"
    |> Seq.iter Shell.cleanDir
)

Target.create "Install" (fun _ ->
    DotNet.restore
        (DotNet.Options.withWorkingDirectory __SOURCE_DIRECTORY__)
        "twitcher.sln"
)

Target.create "YarnInstall" (fun _ ->
    Yarn.install id
)

Target.create "Build" (fun _ ->

    // download API config yaml file
    let configFile = "https://raw.githubusercontent.com/alan-turing-institute/dodo/master/config.yml"
    let wc = new System.Net.WebClient()
    wc.DownloadFile(configFile, localConfig)

    let result =
        DotNet.exec
            (DotNet.Options.withWorkingDirectory __SOURCE_DIRECTORY__)
            "fable"
            "webpack --port free -- -p"

    if not result.OK then failwithf "dotnet fable failed with code %i" result.ExitCode
)

Target.create "Watch" (fun _ ->
    let result =
        DotNet.exec
            (DotNet.Options.withWorkingDirectory __SOURCE_DIRECTORY__)
            "fable"
            "webpack-dev-server --port free"

    if not result.OK then failwithf "dotnet fable failed with code %i" result.ExitCode
)

Target.create "DockerBuild" (fun _ ->

    // let result =
    //     DotNet.exec
    //         (DotNet.Options.withWorkingDirectory __SOURCE_DIRECTORY__)
    //         "fable"
    //         "webpack --port free -- -p"
            
    Fake.IO.Shell.copyRecursive "output" deployDir true |> ignore

    // if not result.OK then failwithf "dotnet fable failed with code %i" result.ExitCode
)

// Build order
"Clean"
    ==> "Install"
    ==> "YarnInstall"
    ==> "Build"
    ==> "DockerBuild"

"Watch"
    <== [ "YarnInstall" ]

// start build
Target.runOrDefault "DockerBuild"
