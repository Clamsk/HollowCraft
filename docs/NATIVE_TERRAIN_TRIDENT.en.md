# Native alpha.10: native terrain and trident aiming

[简体中文](NATIVE_TERRAIN_TRIDENT.md) | English

Alpha.9's synthetic thin-platform case missed Hollow Knight's actual ground-collider type. Reading the locally installed `Tutorial_01` scene confirmed that major terrain chunks use unfilled `EdgeCollider2D` contours. Inferring solid space from a whole collider's bounds and one closest point could discard horizontal floors or fill air. Water then descended beneath the visible ground, leaving an apparent vertical column.

The sampler now reads actual Edge, Box, Polygon, Composite and Circle contours, transforms them to world space and subdivides them per cell. A thin boundary represents edges, and overlap queries include zero-thickness lines on grid boundaries. Shapes and column intersections are reused within each survey. Flow uses combined rectangle coverage and directional channels, records its entry side, and renders segmented bottoms on slopes.

An offline regression uses the actual local tutorial `Chunk 0 1` contour: [numerical fixture](../tools/fixtures/tutorial-edge-floor.json). It checks seven cells of spreading on each side, no downward leak and recovery of a previously stuck saved column. Sloping edges and partially filled polygons are also covered. No Unity/game process ran; this does not replace gameplay acceptance.

Charging previously retained `trident_in_hand` instead of the original `trident_throwing` display. Both hands now switch to the throwing model during use and restore idle afterward. The charging arm uses a forward horizontal baseline plus cursor elevation, removing its extra downward offset. Better Combat attack timing is unchanged.

The main mod builds and 577 pure rule/protocol checks pass. Saves, inventory, blocks and sources are retained; old disconnected leaks recede naturally. Restarting the campaign is unnecessary. Local files and the package are alpha.10; appearance still needs player testing at the same location.
