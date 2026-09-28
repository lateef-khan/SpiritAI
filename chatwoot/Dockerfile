# The pinned Chatwoot image. To update, change the tag, then run `just down && just up` (local)
# or `just prod-update` (production).
# The -ce tag is the MIT community edition, without the separately licensed enterprise folder.
# Put small patches below the FROM line only when config and the API cannot do the job.
FROM chatwoot/chatwoot:v4.18.0-ce

COPY initializers/spirit_public_contact_guard.rb /app/config/initializers/
