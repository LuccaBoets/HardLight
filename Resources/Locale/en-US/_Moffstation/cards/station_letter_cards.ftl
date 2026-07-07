stationletter-card-name-reverse = station letter card
stationletter-card-desc-reverse = The NanoTrasen crest etched on the back gives nothing away...
stationletter-card-value-name = { $card ->
    [detective] 0: Detective
    [security_officer] 1: Security Officer
    [chaplain] 2: Chaplain
    [head_of_security] 3: Head of Security
    [medical_doctor] 4: Medical Doctor
    [clown] 5: Clown
    [nanotrasen_representative] 6: Nanotrasen Representative
    [head_of_personnel] 7: Head of Personnel
    [captain] 8: Captain
   *[other] {$card}
}
stationletter-card-name = {$card}
stationletter-card-desc = { $id ->
    [detective]
        This card has no effect when played or discarded.
        At the end of the round, if you are the only player still in the round who played or discarded this card, you gain one favor token.
        This does not count as winning the round; the winner (even if it is you) still gains their token.
        Even if you play and/or discard two of this card, you still gain only one token.
    [security_officer]
        Choose another player and name a character other than this card.
        If the chosen player has that card in their hand, they are out of the round.
    [chaplain]
        Choose another player and secretly look at their hand, without revealing it to anyone else.
    [head_of_security]
        Choose another player. You and that player secretly compare your hands.
        Whoever has the lower-value card is out of the round.
        If there is a tie, neither player is out of the round.
    [medical_doctor]
        Until the start of your next turn, other players cannot choose you for their card effects.
        If all other players still in the round are protected by this card when you play a card, do the following:
        If that card requires you to choose another player, your card is played with no effect.
        If that card requires you to choose any player, you must choose yourself for the effect.
    [clown]
        Choose any player, including yourself. That player discards their hand without resolving its effect and draws a new hand.
        If the deck is empty, the chosen player draws the facedown set-aside card.
        If a player chooses you for this effect, and you are forced to discard the Captain, you are out of the round.
    [nanotrasen_representative]
        Choose another player and trade hands with that player.
    [head_of_personnel]
        This card has no effect when played or discarded.
        You must play this card as the card for your turn if either the Nanotrasen Representative or the Clown is the other card in your hand.
        You can still choose to play this card even if you do not have the Nanotrasen Representative or the Clown.
        When you play this card, do not reveal your other card; the other players will not know if you were forced to play it or chose to play it.
    [captain]
        If you either play or discard the Captain for any reason, you are immediately out of the round.
   *[invalid] !!invalid!!
}
stationletter-card = Station Letter Card