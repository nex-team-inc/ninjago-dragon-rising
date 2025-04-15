.PHONY: default
default: setup

.PHONY: setup
setup: google-sheet-service-decrypt

.PHONY: google-sheet-service-encrypt
google-sheet-service-encrypt:
	find ./Starter/Assets/Editor/secrets/GoogleSheetsService* -type f \( ! -iname "*.enc" \) -print -exec \
		gcloud kms encrypt \
			--plaintext-file="{}" \
			--ciphertext-file="{}.enc" \
			--key=arcade-xp-server \
			--keyring=arcade-xp-server \
			--location=global \
			--project=arcade-17f17 \;

.PHONY: google-sheet-service-decrypt
google-sheet-service-decrypt:
	find ./Starter/Assets/Editor/secrets/GoogleSheetsService* -type f \( -iname "*.enc" \) -print | \
		sed -e 's/.enc//' | \
		xargs -t -I {} -n 1 gcloud kms decrypt \
			--plaintext-file="{}" \
			--ciphertext-file="{}.enc" \
			--key=arcade-xp-server \
			--keyring=arcade-xp-server \
			--location=global \
			--project=arcade-17f17 \
