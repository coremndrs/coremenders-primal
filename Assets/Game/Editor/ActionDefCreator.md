# Action Verb Creator (Editor tool)

An Editor-only window that creates a **generic action "verb"** — an `ActionDef` asset holding only
`actionId`, `outcome`, and `context`.

- **Window:** `ActionDefCreatorWindow.cs`
- **Assembly:** `Game.Editor.asmdef` (Editor platform only)
- **Open with:** menu **Coremenders ▸ Tools ▸ Action Verb Creator**

---

## The model (TDD §1.12)

Actions are **generic verbs shared across items** — one `Split`, one `Gather`, one `Chop`, reused
everywhere. A verb carries only:

| Field | Type | Notes |
|---|---|---|
| `actionId` | string | `pickup` / `split` / `gather` / `process` … used for labels + prerequisite matching |
| `outcome` | `ActionOutcome` | `ToInventory` / `Transform` / `Carry` / `Consume` (drives instant dispatch) |
| `context` | `ActionContext` (flags) | `World` and/or `Inventory` |

**All per-item parameters** — `requiredLabor`, `yields`, `depletionSpawns`, `effects`
(costs/rewards), `allowedToolCategoryIds`, `toolRequired`, `prerequisite`, `onDepletion`,
`coop`, `labelOverride` — live on each **item Def's `actions[]` list** as an `ItemAction` binding,
authored in the Def Inspector. There are **no per-item ActionDef assets**. The same verb costs and
yields different things on different items because the numbers come from the item, not the verb.

## How to use it

1. Open **Coremenders ▸ Tools ▸ Action Verb Creator**.
2. Enter the `actionId`, pick `outcome` and `context`.
3. Click **Create Verb** → writes `Assets/Game/Data/ActionDefs/ActionDef_<Name>.asset` (overwrite
   toggle appears if it already exists).

Verbs can equally be created via right-click **Create ▸ Coremenders ▸ Action Verb** and edited in
the Inspector (only three fields).

## Binding a verb to an item

In the item's **Def** Inspector → **Actions**, add an `ItemAction` entry, set its **action** to the
verb, then fill in this item's labor / yields / **effects (costs & rewards)** / tool / prerequisite.
For the tree chain, `TreeChainBuilder` (Coremenders ▸ Tools ▸ Build Tree-Chain Defs + ActionDefs)
does this automatically; `PickupActionAssigner` (Coremenders ▸ Tools ▸ Unify Item Pickup Actions)
adds the shared `pickup` binding to every inventory-capable Def.
