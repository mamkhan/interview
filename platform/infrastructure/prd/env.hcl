# Environment-wide facts for production (platform layer). Prepared, not applied. Every environment runs in North
# Europe. Before first apply: production subscription with its own state backend and deployer principal, and a real
# operations distribution list. What differs per resource is a file under variables/<type>/.
locals {
  location = "northeurope"

  # The pipeline principal (workload identity federation service connection) that uploads packages.
  deployer_object_id = "00000000-0000-0000-0000-000000000002"

  # The acceptance suite publishes the V1 reader's events onto the platform's topics as the deployer principal.
  # Production grants nobody standing send access.
  bus_sender_object_ids = []
}
