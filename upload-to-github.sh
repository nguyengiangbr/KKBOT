#!/bin/bash
# KKBOT GitHub Upload Script
# Repository: https://github.com/nguyengiangbr/KKBOT
# Usage: ./upload-to-github.sh [commit_message]

set -e

REPO_URL="https://github.com/nguyengiangbr/KKBOT.git"
REPO_NAME="KKBOT"
COMMIT_MESSAGE="${1:-chore: upload KKBOT platform v24.0.0 - Complete platform with 24 steps}"

echo "🚀 KKBOT GitHub Upload Script"
echo "Repository: https://github.com/nguyengiangbr/KKBOT"
echo "Commit message: $COMMIT_MESSAGE"
echo ""

# Check if we're in a git repository
if [ ! -d ".git" ]; then
    echo "📦 Initializing git repository..."
    git init
    git branch -M main
fi

# Check if remote origin exists
if ! git remote get-url origin > /dev/null 2>&1; then
    echo "🔗 Adding remote origin..."
    git remote add origin https://github.com/nguyengiangbr/KKBOT.git
else
    echo "🔗 Remote origin already exists"
    git remote set-url origin https://github.com/nguyengiangbr/KKBOT.git
fi

# Show current status
echo ""
echo "📊 Current git status:"
git status --short

# Add all files
echo ""
echo "📦 Adding all files to git..."
git add .

# Show what will be committed
echo ""
echo "📝 Files to be committed:"
git diff --cached --stat

# Commit
echo ""
echo "💾 Committing changes..."
git commit -m "$COMMIT_MESSAGE"

# Push to GitHub
echo ""
echo "🚀 Pushing to GitHub..."
git push -u origin main --force-with-lease

echo ""
echo "✅ Upload complete!"
echo "🌐 Repository: https://github.com/nguyengiangbr/KKBOT"
echo "🔗 GitHub Actions: https://github.com/nguyengiangbr/KKBOT/actions"
echo "📦 Releases: https://github.com/nguyengiangbr/KKBOT/releases"