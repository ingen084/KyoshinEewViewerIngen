pipeline {
  agent any
  stages {
    stage('buld win10single') {
      steps {
        bat 'publish_custom.bat win10-x64 single false true'
      }
    }

    stage('buld win10merged') {
      steps {
        bat 'publish_custom.bat win10-x64 merged true true'
      }
    }

    stage('buld osxmerged') {
      steps {
        bat 'publish_custom.bat osx-x64 merged true false'
      }
    }

    stage('buld linuxmerged') {
      steps {
        bat 'publish_custom.bat linux-x64 merged true false'
      }
    }

    stage('publish') {
      steps {
        archiveArtifacts(artifacts: 'tmp/KyoshinEewViewer_ingen_*.zip', onlyIfSuccessful: true)
      }
    }
  }
}