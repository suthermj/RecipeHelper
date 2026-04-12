# TODO

## UI / Navigation

- [ ] Change "Dinners" nav label to "Meal Plan"
- [ ] Add "Add to Cart" / "Add to List" button on individual recipe pages
- [ ] Print recipe feature
- [ ] Share recipe feature

## Meal Planning

- [ ] Enable meal plan history — view previous weeks' meal plans
- [ ] Creating a meal plan should automatically generate the corresponding shopping list

## Shopping List

- [ ] Shopping list integration with Kroger
  - [ ] Select the Kroger store you're shopping at via Kroger API
  - [ ] Add location services to easily search for nearby Kroger stores
  - [ ] Auto-detect store location change — if a different Kroger store is detected, prompt user to update store so aisle locations stay accurate
  - [ ] Attach estimated prices to list items (query Kroger API); auto-apply 10% discount on Kroger brand products (employee discount)
  - [ ] Sort list items by aisle number; produce, meat, and dairy should each be their own separate sections (not sorted with general aisles)
  - [ ] Allow quantity updates on list items after the list is generated
  - [ ] Allow checking off items as completed; completed items move to a "Completed" section at the bottom
- [ ] When generating a list/adding to cart, allow auto-exclusion of bulk pantry items (e.g. spices) that don't need to be purchased every trip

## Recipe Detail Page

- [ ] Fix double-numbered instructions — steps show both a numbered bubble and a leading number in the text (e.g. "1. 1 Cook sausage")
- [ ] Improve ingredient list visual design — add clearer separation between quantity/unit and ingredient name to reduce wall-of-text feel
- [ ] Scalable servings — allow adjusting serving count and have quantities scale accordingly

## Meal Plan Page

- [ ] Show recipe image cards on the weekly dinner selection page instead of plain checkbox list

## Bug Fixes / Improvements

- [ ] Fix "Loading…" text that persists in the footer and never resolves
- [ ] Fix plural logic — "1 teaspoons of sugar" should display as "1 teaspoon of sugar"
- [ ] Improve Kroger product search when linking ingredients — strip quantity/unit prefix so "1 tsp of sugar" searches as "sugar" instead of the full string
