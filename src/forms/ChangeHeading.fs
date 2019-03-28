module Twitcher.HeadingForm

open Twitcher.Domain
open Twitcher.Form

open Elmish
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fulma
open Fulma.FontAwesome


open Elmish.React

open Fable.Import
open Fable.PowerPack
open Fable.Core.JsInterop
open Thoth.Json
open Fable.PowerPack.Fetch.Fetch_types

type FormModel = 
  { AircraftID : string
    CurrentHeading : Heading option
    NewHeading : string
    CheckFields : bool
   }

type Msg = 
  | ChangeHeading of string
  | SubmitForm
  | Cancel
  | Error
  | CheckFields

type ExternalMsg =
    | NoOp
    | Submit of AircraftID * Heading 
    | Cancel

let init(aircraftID, heading) =
  { AircraftID = aircraftID
    CurrentHeading = heading
    NewHeading = ""
    CheckFields = false },
  Cmd.none    


let update msg model =
  match msg with
  | ChangeHeading x ->
      { model with NewHeading = x}, Cmd.none, NoOp

  | SubmitForm ->
      { model with CheckFields = true}, 
      Cmd.ofMsg CheckFields,
      NoOp

  | Msg.Cancel ->
      model, Cmd.none, ExternalMsg.Cancel

  | Error

  | CheckFields ->
      if (model.NewHeading |> checkFloat) then
        let heading = float model.NewHeading % 360.
        model, Cmd.none, ExternalMsg.Submit (model.AircraftID, heading)
      else
        model, Cmd.none, NoOp


let view model (dispatch: Msg -> unit) =
  Modal.modal [ Modal.IsActive true ]
      [ Modal.background [ Props [ OnClick (fun _ -> dispatch Msg.Cancel) ] ] [ ]
        Modal.content [ ]
          [ Box.box' [ ] [
              Heading.p [ Heading.Is5 ] [ str "Change heading" ]
              form [ ]
                [ // TODO display current heading
                  formItem
                    "Heading" 
                    model.NewHeading
                    ChangeHeading
                    model.CheckFields
                    checkFloat
                    "Heading must be a number"
                    (Some (str "Heading is the clock-wise angle to North." ))
                    dispatch          
                ]
              hr []
              Button.button 
                  [ Button.OnClick (fun _ -> dispatch SubmitForm)
                    Button.Color Color.IsPrimary ]
                  [str "Submit"]
              Button.button 
                  [ Button.OnClick (fun _ -> dispatch Msg.Cancel)
                    Button.Color Color.IsGrey ]
                  [str "Cancel"]         
          ]
          ]
        Modal.close [ Modal.Close.Size IsLarge
                      Modal.Close.OnClick (fun _ -> dispatch Msg.Cancel) ] [ ] ]   

