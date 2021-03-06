module Twitcher.Update

open Elmish
open Fable.React
open Fable.React.Props
open Elmish.React

open Fable.Import
open Fable.Core.JsInterop
open Thoth.Json

open Twitcher
open Twitcher.Domain
open Twitcher.Model
open Twitcher.CoordinateSystem
open Twitcher.Commands
open Fable

open System.Collections.Generic


let delayMsg _ =
  promise {
    do! Promise.sleep 1000
    return ()
  }

let simulationViewHeight() =
  Browser.Dom.window.screen.availHeight
  // Browser.Dom.window.document.getElementById("simulation-viewer").clientWidth

let historyLength = 10000
let historyInterval = 10

let updateSingleHistory (positionHistory: Dictionary<AircraftID, Position []>) (aircraft: AircraftInfo) =
  if positionHistory.ContainsKey aircraft.AircraftID then
    let lastItem = positionHistory.[aircraft.AircraftID].[positionHistory.[aircraft.AircraftID].Length-1]
    positionHistory.[aircraft.AircraftID] <-
        if true then //positionHistory.[aircraft.AircraftID].Length < historyLength then
          Array.append
            positionHistory.[aircraft.AircraftID]
            [|aircraft.Position|]
        else
          Array.append
            positionHistory.[aircraft.AircraftID].[1..]
            [| aircraft.Position |]
  else
    positionHistory.[aircraft.AircraftID] <- [| aircraft.Position|]
  positionHistory

let updateHistory (counter: int, positionHistory: Dictionary<AircraftID, Position []>) (positionInfo: AircraftInfo []) =
  if counter = historyInterval || counter = 0 then
    positionInfo
    |> Array.iter (fun aircraft -> updateSingleHistory positionHistory aircraft |> ignore)
    (1, positionHistory)
  else
    (counter + 1, positionHistory)

let estimateHeading (model: Model) (aircraftID: AircraftID) =
  let currentPosition =
    model.Positions
    |> List.find (fun pos -> pos.AircraftID = aircraftID)
    |> fun info -> info.Position
  if (snd model.PositionHistory).ContainsKey aircraftID then
    let lastPosition =
      let history =
        (snd model.PositionHistory).[aircraftID]
      if history.Length >= 1 then
        let pos = history.[history.Length-1]
        if pos <> currentPosition then Some pos
        else None
      else None

    match lastPosition with
    | None -> None
    | Some position ->
        clockwiseAngle position currentPosition |> Some
  else None

let checkLossOfSeparation viewSize (positionInfo: AircraftInfo []) =
  let onScreen =
    positionInfo
    |> Array.filter (fun pos -> 
        CoordinateSystem.isInViewSector 
          (pos.Position.Coordinates.Longitude, 
           pos.Position.Coordinates.Latitude,
           pos.Position.Altitude) viewSize)

  [| for i1 in 0..onScreen.Length-1 do
      for i2 in i1+1..onScreen.Length-1 do
        if onScreen.[i1] <> onScreen.[i2] &&
           abs(onScreen.[i1].Position.Altitude - onScreen.[i2].Position.Altitude) <= 1000.<ft> &&
           (greatCircleDistance onScreen.[i1].Position onScreen.[i2].Position) <= (5.<nm> |> Conversions.Distance.nm2m)
        then
          yield  onScreen.[i1].AircraftID
          yield onScreen.[i2].AircraftID |]

let rescaleVisualisationToSector sv =
  let sectorViewRatio = 
    let width = abs(fst sv.DisplayArea.BottomLeft - fst sv.DisplayArea.TopRight)
    let height = abs(snd sv.DisplayArea.BottomLeft - snd sv.DisplayArea.TopRight)
    width/height

  let sv' = 
    let screenHeight = Browser.Dom.window.screen.availHeight
    let visHeight = 0.6 * screenHeight
    let visWidth = max visHeight (sectorViewRatio*visHeight)
    { sv with VisualisationViewSize = visWidth, visHeight }
  sv'
    

let inSector (sector : DisplayAreaMercator) (position: Position) =
  let x,y = CoordinateSystem.Mercator.lonLatToXY (float position.Coordinates.Longitude) (float position.Coordinates.Latitude)
  let x1,y1 = sector.BottomLeft
  let x2,y2 = sector.TopRight
  x >= x1 && x <= x2 && y >= y1 && y <= y2          


let update (msg:Msg) (model:Model) : Model * Cmd<Msg> =
    match msg with
    | Init ->
        model,
        Cmd.batch [
          getConfigCmd()
          Cmd.ofMsg GetSimulationViewSize
        ]

    | ReadJsonErrorr -> 
        printfn "Error reading JSON from file"
        model, Cmd.none

    | ReadSectorDefinition content ->
        model,
        readSectorDefinitionCmd model.Config.Value content

    | UploadSector sectorJson ->
        model,
        uploadSectorOutlineCmd model.Config.Value sectorJson

    | SectorUploaded status ->
        model, Cmd.ofMsg LoadSector

    | LoadSector ->
        model,
        loadSectorOutlineCmd model.Config.Value

    | SectorOutline sectorOutline ->

        match sectorOutline with
        | Some outline ->

          let maxLat = outline.Coordinates |> Array.map (fun c -> float c.Latitude) |> Array.max |> Mercator.latToY
          let minLat = outline.Coordinates |> Array.map (fun c -> float c.Latitude) |> Array.min |> Mercator.latToY
          let maxLon = outline.Coordinates |> Array.map (fun c -> float c.Longitude) |> Array.max |> Mercator.lonToX
          let minLon = outline.Coordinates |> Array.map (fun c -> float c.Longitude) |> Array.min |> Mercator.lonToX
          let minAlt = outline.BottomAltitude |> Conversions.Altitude.fl2ft
          let maxAlt = outline.TopAltitude |> Conversions.Altitude.fl2ft
          let xwidth = maxLon - minLon |> abs
          let ywidth = maxLat - minLat |> abs
          let height = maxAlt - minAlt
          let centreLat = minLat + 0.5*ywidth
          let centreLon = minLon + 0.5*xwidth
          
          let maxSize = 0.5 * (max xwidth ywidth)
          let displayMargin = model.SimulationZoom  // how much space around the sector should be visible
          let altDisplayMargin = 0.1

          let bottomLeft = centreLon - (1.0 + displayMargin) * maxSize, centreLat - (1.0 + displayMargin) * maxSize
          let topRight = centreLon + (1.0 + displayMargin) * maxSize, centreLat + (1.0 + displayMargin) * maxSize

          let displayArea = {
            BottomLeft = bottomLeft
            TopRight = topRight
            BottomAltitude = minAlt - altDisplayMargin * height
            TopAltitude = maxAlt + altDisplayMargin * height
          }

          { model with 
              SectorInfo = Some outline; 
              DisplayView = { model.DisplayView with DisplayArea = displayArea}},
          Cmd.none
        
        | None -> model, Cmd.none

    | ShowWaypoints x -> { model with ShowWaypoints = x }, Cmd.none

    | ZoomIn ->
        { model with SimulationZoom = model.SimulationZoom - 0.1 }, 
        Cmd.ofMsg (SectorOutline model.SectorInfo)

    | ZoomOut ->    
        { model with SimulationZoom = model.SimulationZoom + 0.1 }, 
        Cmd.ofMsg (SectorOutline model.SectorInfo)

    | ConnectionActive result ->
        if result then
          { model with State = ActiveSimulation(Observing) }, Cmd.none
        else
          { model with State = ConnectionFailed}, Cmd.none

    | Config config ->
       { model with Config = Some config },
       pingBluebirdCmd config

    | GetSimulationViewSize ->
        let viewWidth = simulationViewHeight()
        let x,y = model.DisplayView.VisualisationViewSize

        // { model with
        //     DisplayView = { model.DisplayView with VisualisationViewSize = viewWidth, y } |> rescaleVisualisationToSector
        //     SeparationDistance =
        //       let x1, y1 = rescaleSectorToView TopDown calibrationPoint1 model.DisplayView
        //       let x2, y2 = rescaleSectorToView TopDown calibrationPoint2 model.DisplayView
        //       Some(y1 - y2)
        //   },
        // Cmd.none
        { model with
            DisplayView = { model.DisplayView with VisualisationViewSize = x, viewWidth } |> rescaleVisualisationToSector
            SeparationDistance =
              let x1, y1 = rescaleSectorToView TopDown calibrationPoint1 model.DisplayView
              let x2, y2 = rescaleSectorToView TopDown calibrationPoint2 model.DisplayView
              Some(y1 - y2)
          },
        Cmd.none
    | ChangeDisplay sectorDisplay ->
        { model with SectorDisplay = sectorDisplay },
        Cmd.none        

    | ViewAircraftDetails aircraftID ->
        { model with ViewDetails = Some(aircraftID, None) }, 
        getRouteInfoCmd model.Config.Value aircraftID

    | RouteInfo (aircraftID, route) ->
        { model with ViewDetails = Some(aircraftID, route) },
        Cmd.none

    | CloseAircraftDetails ->
        { model with ViewDetails = None }, Cmd.none

    | GetAllPositions ->
        match model.Config with
        | None ->
            Fable.Core.JS.console.log("No configuration found")
            model, Cmd.none
        | Some config ->
            model,
            getAllPositionsCmd config

    | GetPosition aircraftID ->
        match model.Config with
        | None ->
            Fable.Core.JS.console.log("No configuration found")
            model, Cmd.none
        | Some config ->
            model,
            getAircraftPositionCmd config aircraftID

    | FetchedAllPositions (positionInfo, elapsed) ->
        let aircraftInView = 
          positionInfo
          |> Array.filter (fun ac ->
              inSector model.DisplayView.DisplayArea ac.Position 
          )

        let newModel =
          // { model with Positions = aircraftInView |> List.ofArray }
          { model with Positions = positionInfo |> List.ofArray }

        { newModel with
            // Positions =
            //   newModel.Positions
            //   |> List.map (fun ac ->
            //       { ac with Heading = estimateHeading newModel ac.AircraftID})
            PositionHistory = updateHistory model.PositionHistory aircraftInView
            InConflict = checkLossOfSeparation model.DisplayView aircraftInView
            SimulationTime = elapsed } ,
        Cmd.none

    | FetchedPosition positionInfo ->
        match positionInfo with
        | Some position ->
          Fable.Core.JS.console.log(position)
        | None ->
          Fable.Core.JS.console.log("Aircraft not found")

        model,
        Cmd.none

    // | ShowLoadScenarioForm ->
    //     let f, cmd = ScenarioForm.init()
    //     { model with FormModel = Some (LoadScenarioForm(f)) },
    //         Cmd.batch [
    //           Cmd.map LoadScenarioMsg cmd
    //         ]

    | ReadScenario content ->
        model,
        readScenarioFileCmd model.Config.Value content        

    | UploadScenario path ->
        { model with
              State = Connected
              FormModel = None
              ViewDetails = None
              PositionHistory = 0, (Dictionary<AircraftID, Position []>())
              Positions = []
              InConflict = [||] },
        loadScenarioCmd model.Config.Value path

    | LoadedScenario response ->
        // pause the scenario
        { model with
            State = ActiveSimulation Observing },
        Cmd.none
        //pauseSimulationCmd model.Config.Value

    | ResetSimulator ->
        model,
        Cmd.batch [
          resetSimulatorCmd model.Config.Value
          Cmd.ofMsg CloseAircraftDetails
        ]

    | ResetedSimulator result ->
        if not result then
          Fable.Core.JS.console.log("Failed to reset the simulator")
          model, Cmd.none
        else
          { model with
              State = Connected
              FormModel = None
              ViewDetails = None
              PositionHistory = 0, (Dictionary<AircraftID, Position []>())
              Positions = []
              InConflict = [||]
           }, Cmd.none

    | PauseSimulation ->
        model, pauseSimulationCmd model.Config.Value

    | PausedSimulation result ->
        if not result then
          Fable.Core.JS.console.log("Failed to pause the simulation")
          model, Cmd.none
        else
          { model with State = ActiveSimulation Paused; SimulationSpeed = 1.0 }, Cmd.ofMsg StopAnimation

    | ResumeSimulation ->
        model, resumeSimulationCmd model.Config.Value

    | ResumedSimulation result ->
        if not result then
          Fable.Core.JS.console.log("Failed to resume the simulation")
          model, Cmd.none
        else
          { model with State = ActiveSimulation Playing; SimulationSpeed = 1.0 }, Cmd.ofMsg StartAnimation

    | SetSimulationRateMultiplier rt ->
        model, changeSimulationRateMultiplierCmd model.Config.Value rt

    | ChangedSimulationRateMultiplier result ->
        match result with
        | None ->
          Fable.Core.JS.console.log("Failed to change simulation rate multiplier")
          model, Cmd.none
        | Some rt ->
          { model with SimulationSpeed = rt }, Cmd.none

    | Observe ->
        { model with
              FormModel = None
              ViewDetails = None
              PositionHistory = 0, (Dictionary<AircraftID, Position []>())
              Positions = []
              InConflict = [||]
              State = ActiveSimulation Observing },
        Cmd.ofMsg StartAnimation

    | GetSimulationInfo ->
        model, 
        getSimulationInfoCmd model.Config.Value

    | SimulationInfo info ->
        { model with 
            SimulationInfo = info
            SimulationSpeed = if info.IsSome then float info.Value.Speed else model.SimulationSpeed },
        Cmd.none

    | MakeSimulatorStep ->
        match model.State with
        | ActiveSimulation(_) ->
          { model with State = ActiveSimulation(TakingStep) },
          makeSimulationStepCmd model.Config.Value
        | _ ->
          model, Cmd.none 

    | SimulatorStepTaken () ->
        { model with State = ActiveSimulation Observing }, Cmd.none        
        // TODO: potentially need to decide if Observing or Playing \
        // when sandbox mode enabled

    | StopObserving ->
        { model with State = Connected },
        Cmd.ofMsg StopAnimation

    | CreateAircraft aircraftInfo ->
        model, createAircraftCmd model.Config.Value aircraftInfo

    | CreatedAircraft result ->
        Fable.Core.JS.console.log(result)
        model, Cmd.none

    | ChangeAltitude (aircraftID, requestedAltitude, verticalSpeed) ->
        model, changeAltitudeCmd model.Config.Value aircraftID requestedAltitude verticalSpeed

    | ChangedAltitude result ->
        Fable.Core.JS.console.log(result)
        model, Cmd.none

    | ChangeHeading (aircraftID, requestedHeading) ->
        model, changeHeadingCmd model.Config.Value aircraftID requestedHeading

    | ChangedHeading result ->
        Fable.Core.JS.console.log(result)
        model, Cmd.none

    | ChangeSpeed (aircraftID, speed) ->
        model, changeSpeedCmd model.Config.Value aircraftID speed
    | ChangedSpeed result ->
        Fable.Core.JS.console.log(result)
        model, Cmd.none

    | ChangeVerticalSpeed (aircraftID, verticalSpeed) -> model, Cmd.none
    | ChangedVerticalSpeed  -> model, Cmd.none


    | ConnectionError exn | ErrorMessage exn ->
        Fable.Core.JS.console.error(exn)
        model,
        Cmd.none

    | MakeAnimationStep _ ->
        if model.Animate then
          model,
          Cmd.batch [
           getAllPositionsCmd model.Config.Value
           Cmd.OfPromise.either delayMsg () MakeAnimationStep ErrorMessage
           Cmd.ofMsg GetSimulationViewSize
           (if model.SectorInfo.IsNone then Cmd.ofMsg LoadSector else Cmd.none)
          ]
        else
          model,
          Cmd.none

    | StartAnimation ->
        { model with Animate = true }, Cmd.ofMsg (MakeAnimationStep())

    | StopAnimation ->
        { model with Animate = false }, Cmd.none

    | ShowCreateAircraftForm ->
        let f, cmd = AircraftForm.init()
        { model with FormModel = Some (CreateAircraftForm(f)) },
            Cmd.batch [
              Cmd.map CreateAircraftMsg cmd
            ]

    | ShowChangeAltitudeForm aircraft ->
        let f, cmd = AltitudeForm.init(aircraft.AircraftID, aircraft.Position.Altitude)
        { model with FormModel = Some (ChangeAltitudeForm(f)) },
            Cmd.batch [
              Cmd.map ChangeAltitudeMsg cmd
            ]

    | ShowChangeSpeedForm aircraft ->
        let f, cmd = SpeedForm.init(aircraft.AircraftID, aircraft.GroundSpeed)
        { model with FormModel = Some (ChangeSpeedForm(f)) },
            Cmd.batch [
              Cmd.map ChangeSpeedMsg cmd
            ]

    | ShowChangeHeadingForm aircraft ->
        let f, cmd = HeadingForm.init(aircraft.AircraftID, aircraft.Heading)
        { model with FormModel = Some (ChangeHeadingForm(f)) },
            Cmd.batch [
              Cmd.map ChangeHeadingMsg cmd
            ]

    | CreateAircraftMsg m ->
      match model.FormModel with

      | Some(CreateAircraftForm f) ->
          let f', cmd, externalMsg = AircraftForm.update m f

          match externalMsg with
          | AircraftForm.ExternalMsg.Submit info ->
              { model with FormModel = None },
              Cmd.batch [
                Cmd.ofMsg (CreateAircraft info)
              ]

          | AircraftForm.ExternalMsg.NoOp ->
              { model with FormModel = Some (CreateAircraftForm(f')) },
              Cmd.map CreateAircraftMsg cmd

          | AircraftForm.ExternalMsg.Cancel ->
              { model with FormModel = None },
              Cmd.none

      | None | Some _ ->
          let f, cmd = AircraftForm.init()
          { model with FormModel = Some (CreateAircraftForm(f)) },
              Cmd.batch [
                Cmd.map CreateAircraftMsg cmd
                Cmd.ofMsg (CreateAircraftMsg m) // initialized, resend
              ]

    | ChangeAltitudeMsg m ->
      match model.FormModel with

      | Some(ChangeAltitudeForm f) ->
          let f', cmd, externalMsg = AltitudeForm.update m f

          match externalMsg with
          | AltitudeForm.ExternalMsg.Submit(acid,alt,vs) ->
              { model with FormModel = None },
              Cmd.batch [
                Cmd.ofMsg (ChangeAltitude (acid,alt,vs))
              ]

          | AltitudeForm.ExternalMsg.NoOp ->
              { model with FormModel = Some (ChangeAltitudeForm(f')) },
              Cmd.map ChangeAltitudeMsg cmd

          | AltitudeForm.ExternalMsg.Cancel ->
              { model with FormModel = None },
              Cmd.none

      | None | Some _ ->
          Fable.Core.JS.console.log("Error - incorrect form model")
          { model with FormModel = None }, Cmd.none

    | ChangeSpeedMsg m ->
      match model.FormModel with

      | Some(ChangeSpeedForm f) ->
          let f', cmd, externalMsg = SpeedForm.update m f

          match externalMsg with
          | SpeedForm.ExternalMsg.Submit(acid,cas) ->
              { model with FormModel = None },
              Cmd.batch [
                Cmd.ofMsg (ChangeSpeed (acid,cas))
              ]

          | SpeedForm.ExternalMsg.NoOp ->
              { model with FormModel = Some (ChangeSpeedForm(f')) },
              Cmd.map ChangeSpeedMsg cmd

          | SpeedForm.ExternalMsg.Cancel ->
              { model with FormModel = None },
              Cmd.none

      | None | Some _ ->
          Fable.Core.JS.console.log("Error - incorrect form model")
          { model with FormModel = None }, Cmd.none


    | ChangeHeadingMsg m ->
      match model.FormModel with

      | Some(ChangeHeadingForm f) ->
          let f', cmd, externalMsg = HeadingForm.update m f

          match externalMsg with
          | HeadingForm.ExternalMsg.Submit(acid,heading) ->
              { model with FormModel = None },
              Cmd.batch [
                Cmd.ofMsg (ChangeHeading (acid,heading))
              ]

          | HeadingForm.ExternalMsg.NoOp ->
              { model with FormModel = Some (ChangeHeadingForm(f')) },
              Cmd.map ChangeHeadingMsg cmd

          | HeadingForm.ExternalMsg.Cancel ->
              { model with FormModel = None },
              Cmd.none

      | None | Some _ ->
          Fable.Core.JS.console.log("Error - incorrect form model")
          { model with FormModel = None }, Cmd.none



