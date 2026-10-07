# Invoice withholding deduction sign

Source checkpoint InvoiceGenerator prints a minus for positive withholding. Current renderer passed positive withholding unchanged, unlike quotation/receipt numeric subtraction. Render numeric negation for invoice while preserving accepted zero-visible/null-unknown omission from PR46 and accepted negative inputs. Caller-supplied Outstanding remains authoritative; Accounting producer computes it before persistence and sends it unchanged. No invented renderer recalculation when supplied fields differ.

Four actual Production RS256 HTTP/PDF cases positive3/negative3/zero/null verify the withholding amount on its PDF baseline to the right of the label; no duplicate minus, and suppliedOutstanding91.23 remains unchanged despiteTotal107. Existing three zero/nullroute tests,22immutablebaselines/fontresources retained. Fullforecast373, focused160/runtime43 unchanged because new class is outside focused filter. Exactfullinventory and syntheticpartial-suite message updated; strictguards unchanged.

Original source only displays positive withholding rows and computes roundedOutstanding in that branch. Accepted zero/null distinction and caller-calculation ownership are explicit current compatibility adaptations, not identical original visibility/calculation. No DTOrange restriction, producer/data/provider/deployment mutation.
