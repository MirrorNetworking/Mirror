#!/bin/sh -e

BRANCH=$1
TAG=$2
git subtree split --prefix=Assets/Mirror -b $BRANCH
#git filter-branch --prune-empty --tree-filter 'rm -rf Tests' upm
git gc
git filter-repo --force --invert-paths --path Tests --path Tests.meta --path Examples.meta --path-rename "Examples:Samples~" --refs $BRANCH
git tag $TAG $BRANCH
git push origin $BRANCH --tags
