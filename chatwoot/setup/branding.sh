#!/usr/bin/env bash

# Puts Spirit's name, logo, and links where Chatwoot shows its own. Chatwoot keeps these as
# installation config rows, and its Super Admin page locks them behind a paid plan, so this writes
# the rows from the Rails console instead. The logo is baked into the image by the Dockerfile.

set -euo pipefail

compose="${1:?usage: branding.sh <docker compose command>}"

bash -c "$compose exec -T web bundle exec rails runner -" <<'RUBY'
{
  'INSTALLATION_NAME' => 'Spirit Desk',
  'BRAND_NAME' => 'Spirit Fitness',
  'LOGO' => '/brand-assets/spirit-logo.png',
  'LOGO_DARK' => '/brand-assets/spirit-logo.png',
  'LOGO_THUMBNAIL' => '/brand-assets/spirit-logo.png',
  'BRAND_URL' => 'https://www.spiritfitness.com',
  'WIDGET_BRAND_URL' => 'https://www.spiritfitness.com'
}.each do |name, value|
  config = InstallationConfig.find_or_initialize_by(name: name)
  config.value = value
  config.locked = false
  config.save!
end
RUBY
echo "Set Chatwoot's name, logo, and brand links to Spirit's." >&2
