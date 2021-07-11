pipeline {
  agent any
  stages {
    stage('buld win10single') {
      steps {
        bat 'publish_custom.bat win10-x64 single false'
      }
    }

    stage('buld win10merged') {
      steps {
        bat 'publish_custom.bat win10-x64 merged true'
      }
    }

    stage('buld osxmerged') {
      steps {
        bat 'publish_custom.bat osx-x64 merged true'
      }
    }

    stage('buld linuxmerged') {
      steps {
        bat 'publish_custom.bat linux-x64 merged true'
      }
    }

    stage('publish') {
      steps {
        archiveArtifacts(artifacts: 'tmp/KyoshinEewViewer_ingen_*.zip', onlyIfSuccessful: true)

        withCredentials([string(credentialsId: 'DISCORD_WEBHOOK', variable: 'WebhookUrl')]) {
          discordSend(description: "ƒrƒ‹ƒh‚ªŠ®—¹‚µ‚Ü‚µ‚½", footer: "Jenkins", link: env.BUILD_URL, result: currentBuild.currentResult, title: JOB_NAME, webhookURL: WebhookUrl)
        }
      }
    }
  }
}