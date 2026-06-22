reagent-effect-guidebook-aphrodisiac =
    { $chance ->
        [1] Causes
        *[other] cause
    } arousal

reagent-effect-guidebook-painkillers = Numbs pain
reagent-effect-guidebook-painkillers-remove = Stops numbing pain

guidebook-reagent-spoil-conditions-header = Spoil Conditions

reagent-effect-guidebook-spoil-conditions =
    { $bloodstreamPreserve ->
        [true] { $allowedBloodTypes ->
            [none] Spoils outside the bloodstream.
            *[other] Spoils outside: { $allowedBloodTypes } bloodstreams.
        }
        *[false] ""
    }
    Spoils into: { $spoilsInto }.
    { $preservedBySpoilageContainers ->
        [true] Preserved by cryostasis containers.
        *[false] Not preserved by cryostasis containers.
    }
    Spoil time: { $spoilTime }.
