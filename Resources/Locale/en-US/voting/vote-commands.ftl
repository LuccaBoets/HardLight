### Voting system related console commands

## 'createvote' command

cmd-createvote-desc = Creates a vote
cmd-createvote-help = Usage: createvote <'restart'|'preset'|'map'>
cmd-createvote-cannot-call-vote-now = You can't call a vote right now!
cmd-createvote-invalid-vote-type = Invalid vote type
cmd-createvote-arg-vote-type = <vote type>

## 'customvote' command

cmd-customvote-desc = Creates a custom vote
cmd-customvote-help = Usage: customvote <title> <option1> <option2> [option3...]
cmd-customvote-on-finished-tie = Tie between {$ties}!
cmd-customvote-on-finished-win = {$winner} wins!
cmd-customvote-arg-title = <title>
cmd-customvote-arg-option-n = <option{ $n }>

## 'vote' command

cmd-vote-desc = Votes on an active vote
cmd-vote-help = vote <voteId> <option>
cmd-vote-cannot-call-vote-now = You can't call a vote right now!
cmd-vote-on-execute-error-must-be-player = Must be a player
cmd-vote-on-execute-error-invalid-vote-id = Invalid vote ID
cmd-vote-on-execute-error-invalid-vote-options = Invalid vote options
cmd-vote-on-execute-error-invalid-vote = Invalid vote
cmd-vote-on-execute-error-invalid-option = Invalid option

## 'listvotes' command

cmd-listvotes-desc = Lists currently active votes
cmd-listvotes-help = Usage: listvotes

## 'votehistory' command

cmd-votehistory-desc = Lists active and recently finished votes
cmd-votehistory-help = Usage: votehistory [count]
                       Shows up to [count] active votes and recent historical votes.
cmd-votehistory-error-invalid-count = Count must be a positive integer
cmd-votehistory-arg-count = [count]
cmd-votehistory-active-header = Active votes:
cmd-votehistory-history-header = Recent finished votes:
cmd-votehistory-empty = (none)

## 'voteinspect' command

cmd-voteinspect-desc = Shows who voted what for an active or recent vote
cmd-voteinspect-help = Usage: voteinspect <id>
                       Use votehistory to find recent vote IDs.
cmd-voteinspect-error-missing-vote-id = Missing vote ID
cmd-voteinspect-error-invalid-vote-id = Invalid vote ID
cmd-voteinspect-arg-id = <id>
cmd-voteinspect-options-header = Options:
cmd-voteinspect-voters-header = Voters:
cmd-voteinspect-no-votes = (no votes cast)
cmd-voteinspect-unknown-option = <unknown option>

## 'cancelvote' command

cmd-cancelvote-desc = Cancels an active vote
cmd-cancelvote-help = Usage: cancelvote <id>
                      You can get the ID from the listvotes command.
cmd-cancelvote-error-invalid-vote-id = Invalid vote ID
cmd-cancelvote-error-missing-vote-id = Missing ID
cmd-cancelvote-arg-id = <id>
