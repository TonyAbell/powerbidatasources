open System
open Microsoft.IdentityModel.Clients.ActiveDirectory
open Microsoft.Extensions.Configuration
open System.Net
open System.IO
open Newtonsoft.Json.Linq

// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

[<EntryPoint>]
let main argv = 
   
    
    let builder = ConfigurationBuilder().AddJsonFile("appsettings.json")
    let config = builder.Build()

    let clientId = config.["clientId"] 


    let authority = @"https://login.windows.net/common/oauth2/authorize/"
    let authenticationContext = new AuthenticationContext(authority, false)
    let resource = "https://analysis.windows.net/powerbi/api"
    let redirectUri = new Uri("http://localhost/")
    let platformParameters = Microsoft.IdentityModel.Clients.ActiveDirectory.PlatformParameters( PromptBehavior.Auto)
    let token = authenticationContext.AcquireTokenAsync(resource, clientId, redirectUri, platformParameters).Result
    let accessToken = token.AccessToken
   
    let getDataAsJArray (url:string)  = 
        try 
            let request = WebRequest.CreateHttp(url)
            request.Method <- "GET"
            request.ContentType <- "application/json"
            request.Headers.Add("Authorization",(sprintf "Bearer %s" accessToken))
            use response = request.GetResponse()
            let array = 
                    (new StreamReader( response.GetResponseStream())).ReadToEnd() 
                    |> JObject.Parse
                    |> fun f -> f.GetValue("value") :?> JArray
            //printfn "Data:\n\n%s" (array.ToString(Formatting.Indented))
            array
        with 
        | :? WebException as ex -> 
            use stream = ex.Response.GetResponseStream()
            use reader = new StreamReader(stream)
            //printfn "HTTP Status Code:\t%s" ((ex.Response :?> HttpWebResponse).StatusCode.ToString())
            let errorMsg = (reader.ReadToEnd())
            if not (String.IsNullOrEmpty(errorMsg)) then
                //printfn "Error Body: \n\n%s" (JObject.Parse(errorMsg).ToString(Formatting.Indented))
                ()
            JArray()
        | ex -> printfn "\nException\n\t%A" ex
                JArray()
    let getGroups =
        getDataAsJArray "https://api.powerbi.com/v1.0/myorg/groups"
     

    let getDataSets goupdId =
        let uri = sprintf "https://api.powerbi.com/v1.0/myorg/groups/%s/datasets" goupdId
        getDataAsJArray uri
      

    let getDataSource groupdId dataSetId = 
        let uri = sprintf "https://api.powerbi.com/v1.0/myorg/groups/%s/datasets/%s/dataSources" groupdId dataSetId
        getDataAsJArray uri    
     
    
    getGroups |> Seq.iter(fun g-> 
        let groupId = (g.["id"].ToString())
        let groupName =  (g.["name"].ToString())
        printfn "\n\n-----------------------------------------------------"
        //printfn "Group ID: \t%s" groupId
        printfn "Group: \t%s" groupName
        let dataSets = getDataSets groupId
        dataSets 
            |> Seq.iter(fun d ->
                let datasetId = (d.["id"].ToString())
                let datasetName = (d.["name"].ToString())
                let by =  (d.["configuredBy"].ToString())
                // printfn "DataSet ID: \t%s" datasetId
                printfn "\tData Set: %s\tBy: %s" datasetName by
                let dataSource = getDataSource groupId datasetId
                printfn "\t\tData Sources:"
                if dataSource.Count = 0 then 
                    printfn "\t\t\tNo Direct Query Connection On DataSet "
                else
                    dataSource 
                        |> Seq.iter(fun ds ->
                            let connectionString = (ds.["connectionString"].ToString())
                            let dataSourceName = (ds.["name"].ToString())
                            printfn "\t\t\tData Source Name: \t%s" dataSourceName
                            printfn "\t\t\tData Source Cnn: \t%s" connectionString
                        )
            )
        )
    System.Console.ReadLine() |> ignore
    0 // return an integer exit code
