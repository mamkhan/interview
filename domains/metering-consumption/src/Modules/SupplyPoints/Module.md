# SupplyPoint

What ESB states about each supply point: the meter point's codes and networks reference number, the usage factors,
and the meters in place with their registers and reads.

Store reads the one configuration change Market Messaging publishes per file on SupplyPoints' own topic and stores it
whole or not at all. Each kind of fact has a history, only what each statement stated on its date, and a current
table, every value in effect from today's row and the rows already known for later days. A statement that changes
nothing writes nothing; a backdated value reaches the later rows that only carried an earlier one. Reload replaces a
supply point's codes and usage factors with V1's when the V1 reader loads it. Provide answers what was in effect on a
day, from current when it reaches back that far and from history otherwise. Owned facts: the supply point and its
history and current tables in the `supply` schema. Published contracts: the in-effect read for other modules.

Non-goals: parsing market messages, the customer, the address, move-in and move-out, readings and consumption,
tariffs, and anything Consumption's profile keeps today.
