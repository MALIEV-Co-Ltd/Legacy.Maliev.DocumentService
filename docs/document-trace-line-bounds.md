# Bounded diagnostic line admission

The PR47 primary run37508469108 built cleanly and passed365 tests, then its
PostTest capture rejected a33334-character diagnostic line. Fixed structural
facts show no recognized collector marker and neither checked execution-batch
marker. The actual record family and payload are unknown and are not printed.

All diagnostic lines now have an intentional64 KiB character cap. Recognized
collector lines retain the original32 KiB cap in the typed reader; claimed
collector envelopes retain32 KiB in the passive producer observer. No unknown
line is skipped to bypass admission. Existing per-file8 MiB, aggregate32 MiB,
131072-line,512-typed-event,XML and total-read limits are unchanged. The bounded
increase accommodates the observed33334-character record without guessing its
family or accepting arbitrary-size input. It provides no evidence by itself.

Ten synthetic PostTest controls exercise the observed size,64 KiB boundary,
oversize rejection, collector32 KiB rejection, line-count and8 MiB rejection,
known session/PID extraction, unknown relevant refusal to establish completeness,
and unchanged inactive/runtime/raw-export flags. All module/path/identity and
producer-source binding requirements remain necessary and unchanged.
