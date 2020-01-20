semantic-release --dry-run |  %{ 
    echo $_
    $fields = -split $_;
    if ($fields[0] -eq "#" -or $fields[0] -eq "##") {
        echo "Mirror version " $fields[1]
        echo $fields[1] > Assets/Mirror/version.txt
    }
}
