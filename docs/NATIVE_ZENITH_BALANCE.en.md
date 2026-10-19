# Native alpha.11: Zenith damage and phase protection

English | [简体中文](NATIVE_ZENITH_BALANCE.md)

Each complete Zenith attack now deals at most **200 native Hollow Knight HP per target**. An unshielded, damageable 2000-HP target takes about ten landed attacks. Holding the button starts two attacks per second. All three visual swords, trails and sword variations remain; repeated contact, multiple swords and random critical hits no longer multiply a single attack's damage.

Previously each sword repeatedly dealt 190 proxy HP, with possible double damage. Native enemy health is compressed into that proxy scale, so 190 proxy HP could represent substantially more than 190 native HP. This release converts the requested damage using native maximum health. Minecraft creatures receive 200 Minecraft HP per attack.

Existing native phase floors and hit events remain authoritative. Damage clipped by a phase boundary is discarded. Swords from an already accepted attack cannot damage the next phase again after health/generation reconciliation. A new attack can hit normally. Releasing completes the attack already started without queuing another at the cycle boundary. Scene changes cancel swords, queued attacks and held input. No boss states, ending sequences or victory conditions are forced.

Eighteen new offline checks cover ten attacks against a 2000-HP target, phase clipping, reconciliation, sustained cadence, health scales, multiple targets and scene cancellation. All 595 pure rule/protocol checks and the main mod build pass. No game was launched or closed. The reported black screen and complete Absolute Radiance encounter still require player acceptance after restarting that encounter; numerical checks are not a gameplay verification.

The update preserves inventory, loadouts, blocks and saves. Update with the game closed and use the existing launcher. A new adventure save is unnecessary.
