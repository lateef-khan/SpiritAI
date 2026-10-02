---
name: dealer-price
description: >-
  The dealer price of a part, beside its retail price.
---

DEALER PRICE

THE LOOKUP
  dealer_prices   part numbers in, each with Description, RetailPrice and DealerPrice.
                  Up to 50 at once.

The part numbers are the SpNo column of search_parts. Find the parts first, then send all
their SpNo values in ONE dealer_prices call, comma separated. More than 50: send them in
batches of 50.
Found 0 means the catalogue has no part with that number. Say so; never guess a price.
dealer_prices gives these two prices and no other.
