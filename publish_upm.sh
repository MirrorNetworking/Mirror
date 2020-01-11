#!/bin/sh
git subtree split --prefix=Assets/Mirror -b upm 
git filter-branch --prune-empty --tree-filter 'rm -rf Tests' upm
git tag $1 upm
#git push --tags
