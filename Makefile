export VERSION := 0.0.1.19
export GITHUB_REPO := KeksBombe/jellyfin-plugin-auto-collections
export FILE := auto-collections-${VERSION}.zip

build:
	dotnet build

zip:
	zip "${FILE}" Jellyfin.Plugin.AutoCollections/bin/Debug/net8.0/Jellyfin.Plugin.AutoCollections.dll

csum:
	md5sum "${FILE} ""

create-tag:
	git tag ${VERSION}
	git push origin ${VERSION}

create-gh-release:
	gh release create ${VERSION} "${FILE}" --generate-notes --verify-tag

update-version:
	node scripts/update-version.js

update-manifest:
	node scripts/validate-and-update-manifest.js

push-manifest:
	git commit -m 'new release' manifest.json
	git push origin main

release: update-version build zip create-tag create-gh-release update-manifest push-manifest