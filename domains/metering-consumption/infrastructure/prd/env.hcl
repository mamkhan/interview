# Environment-wide facts for production (Metering & Consumption domain). Prepared, not applied. Retention
# and policy locking are compliance decisions to confirm before first apply; the deployer principal
# must become a production-scoped service connection. What differs per resource is a file under
# variables/<type>/.
locals {
  location = "northeurope"

  max_delivery_count = 10

  # The pipeline deployer principal (workload identity federation service connection): it runs the
  # migrations bundle and creates the database users, and Entra-only SQL has one admin slot.
  sql_aad_admin_login = "acme-platform-deployer-00000000-0000-0000-0000-000000000001"
  deployer_object_id  = "00000000-0000-0000-0000-000000000002"

  # The acceptance suite writes its operator file into the drop container as the deployer
  # principal. Production grants nobody standing write access to the evidence account.
  archive_writer_object_ids = []

  # The acceptance suite publishes Profile's input events onto their topics as the deployer principal.
  # Production grants nobody standing send access.
  bus_sender_object_ids = []
}
