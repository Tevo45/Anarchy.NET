module Perks

open Newtonsoft.Json

(* lol-perks *)

(* LolPerksPerkPageResource *)
type Page = { autoModifiedSelections: int32 list
              current: bool
              id: int32
              isActive: bool
              isDeletable: bool
              isEditable: bool
              isValid: bool
              lastModified: int64
              name: string
              order: int32
              primaryStyleId: int32
              selectedPerkIds: int32 list
              subStyleId: int32 }

(* GetLolPerksV1Currentpage *)
let getCurrentPage conn =
    let json = LCU.get conn "/lol-perks/v1/currentpage"
    JsonConvert.DeserializeObject<PageResource>(json)

(* GetLolPerksV1Pages *)
let getPages conn =
    let json = LCU.get conn "/lol-perks/v1/pages"
    JsonConvert.DeserializeObject<PageResource list>(json)

(* GetLolPerksV1PagesById *)
let getPageById conn (id: int32) =
    let json = LCU.get conn $"/lol-perks/v1/pages/{id}"
    JsonConvert.DeserializeObject<PageResource>(json)

(* PostLolPerksV1Pages *)
(*
 * creates a new rune page based on `page`
 * 400 -> no more rune slots (apparently)
 *)
let postPage conn (page: Page) =
    let json = LCU.post conn "/lol-perks/v1/pages" page
    JsonConvert.DeserializeObject<PageResource>(json)
