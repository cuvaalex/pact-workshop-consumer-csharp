# Step 2: Provider (Verifying an existing contract)
When we previously ran (in the consumer) the ```` ConsumerPactTests```` test, it passed, but it also generated a ````pacts/consumer-provider.json```` pact file that we can use to validate our assumptions in the provider side.
Pact has a rake task to verify the provider against the generated pact file. It can get the pact file from any URL (like the last successful CI build), but we are just going to use the local one for now.

Add under  file the following line require 'pact/tasks' so it looks like this:


Now run ```` git checkout step3 ```` to go to the next step
