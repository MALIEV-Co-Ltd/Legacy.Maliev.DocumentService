# Quotation discount adjustment sign

Current nonzero Discount input is accepted, including negative values. Rendering computed price * percentage /100 and prefixed a minus character, causing negative inputs to display a double minus. Render the numeric negation through the existing invariant Money formatter instead: price100/discount1 shows -1.00 THB/UNIT; discount-1 shows1.00 THB/UNIT. The signed percentage label remains unchanged. Zero/null still omit the adjustment and discount label. Caller-provided subtotal/totals and DTO acceptance are unchanged.

Four actual Production RS256 authenticated HTTP/PDF cases exercise positive, negative, zero, and null inputs through the existing runtime host and PdfPig text assertions. They reject a duplicate minus; negative input also rejects a negative adjustment; zero/null omit both labels. Full expected369, with all365 prior identities retained. Existing22 immutable PDF baselines and raster checks remain required. No provider, persistence, or deployment.

Percentage formatting currently uses current-culture N2, while Money uses invariant N2. This slice preserves the percentage format; no broader invariant-percentage contract is asserted or changed.
